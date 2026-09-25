using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using credit_platf.Data;
using credit_platf.Models;
using credit_platf.Services;

namespace credit_platf.Controllers;

// Catalogo de solicitudes del usuario autenticado (P2).
// El UsuarioId siempre sale del servidor (claims); nunca de la URL ni del form.
[Authorize]
public class SolicitudesController(ApplicationDbContext db, UserManager<IdentityUser> users, CacheSolicitudes cacheSol) : Controller
{
    public async Task<IActionResult> Index(SolicitudFiltros filtros)
    {
        var userId = users.GetUserId(User)!;
        var cliente = await db.Clientes
            .Include(c => c.Usuario)
            .FirstOrDefaultAsync(c => c.UsuarioId == userId);

        var vm = new MisSolicitudesViewModel
        {
            Filtros = filtros,
            TieneCliente = cliente is not null
        };

        if (cliente is null)
            return View(vm);

        var errores = filtros.Validar().ToList();
        foreach (var e in errores)
            ModelState.AddModelError(string.Empty, e);

        // Cache Redis 60 s (P4): solo con filtros validos.
        var claveCache = CacheSolicitudes.ClaveListado(userId, filtros);
        if (errores.Count == 0)
        {
            var hit = await cacheSol.LeerAsync(claveCache);
            if (hit is not null)
            {
                vm.Resultados = hit.Select(i => new SolicitudCredito
                {
                    Id = i.Id,
                    ClienteId = cliente.Id,
                    MontoSolicitado = i.MontoSolicitado,
                    FechaSolicitud = i.FechaSolicitud,
                    Estado = i.Estado
                }).ToList();
                ViewData["DesdeCache"] = true;
                return View(vm);
            }
        }

        // Filtro invalido: no se acepta, se muestra la lista completa sin filtrar.
        var query = db.Solicitudes
            .Include(s => s.Cliente)
            .Where(s => s.ClienteId == cliente.Id);

        if (errores.Count == 0 && filtros.TieneAlgunFiltro)
        {
            if (filtros.Estado.HasValue)
                query = query.Where(s => s.Estado == filtros.Estado.Value);
            if (filtros.MontoMin.HasValue)
                query = query.Where(s => s.MontoSolicitado >= filtros.MontoMin.Value);
            if (filtros.MontoMax.HasValue)
                query = query.Where(s => s.MontoSolicitado <= filtros.MontoMax.Value);
            if (filtros.Desde.HasValue)
                query = query.Where(s => s.FechaSolicitud.Date >= filtros.Desde.Value.Date);
            if (filtros.Hasta.HasValue)
                query = query.Where(s => s.FechaSolicitud.Date <= filtros.Hasta.Value.Date);
        }

        vm.Resultados = await query
            .OrderByDescending(s => s.FechaSolicitud)
            .ToListAsync();

        if (errores.Count == 0)
            await cacheSol.GuardarAsync(userId, claveCache,
                vm.Resultados.Select(SolicitudCacheItem.Desde).ToList());
        ViewData["DesdeCache"] = false;

        return View(vm);
    }

    public async Task<IActionResult> Details(int id)
    {
        var userId = users.GetUserId(User)!;
        var solicitud = await db.Solicitudes
            .Include(s => s.Cliente)
            .ThenInclude(c => c!.Usuario)
            .FirstOrDefaultAsync(s => s.Id == id);

        // 404 indistinto para inexistente o ajena: no revelar datos de otros clientes.
        // El rol Analista (P5) podra ver cualquier solicitud.
        if (solicitud is null
            || (solicitud.Cliente?.UsuarioId != userId && !User.IsInRole("Analista")))
            return NotFound();

        // P4 sesion (Redis-backed): ultima solicitud visitada para el layout.
        HttpContext.Session.SetString("UltimaSolicitud",
            JsonSerializer.Serialize(new { id = solicitud.Id, monto = solicitud.MontoSolicitado }));

        return View(solicitud);
    }

    // Registro de solicitud (P3). Crea siempre en estado Pendiente.
    public async Task<IActionResult> Create()
    {
        var vm = await ArmarCreateVmAsync();
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("MontoSolicitado")] SolicitudCreateViewModel form)
    {
        var userId = users.GetUserId(User)!;
        var cliente = await db.Clientes.FirstOrDefaultAsync(c => c.UsuarioId == userId);

        if (cliente is null || !cliente.Activo)
        {
            ModelState.AddModelError(string.Empty,
                "Tu cuenta no tiene un cliente activo asociado; no puedes registrar solicitudes.");
            return View(await ArmarCreateVmAsync());
        }

        var tienePendiente = await db.Solicitudes.AnyAsync(s =>
            s.ClienteId == cliente.Id && s.Estado == EstadoSolicitud.Pendiente);

        var regla = SolicitudReglas.ValidarRegistro(
            form.MontoSolicitado, cliente.IngresosMensuales, cliente.Activo, tienePendiente);
        if (regla is not null)
            ModelState.AddModelError(string.Empty, regla);

        if (!ModelState.IsValid)
            return View(await ArmarCreateVmAsync());

        var solicitud = new SolicitudCredito
        {
            ClienteId = cliente.Id,
            MontoSolicitado = form.MontoSolicitado,
            FechaSolicitud = DateTime.UtcNow,
            Estado = EstadoSolicitud.Pendiente
        };
        db.Solicitudes.Add(solicitud);

        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // Red de seguridad ante doble submit en carrera (indice parcial 1 Pendiente).
            ModelState.AddModelError(string.Empty, "Ya tienes una solicitud en estado Pendiente.");
            return View(await ArmarCreateVmAsync());
        }

        // P4: invalidar cache del listado del usuario.
        await cacheSol.InvalidarUsuarioAsync(userId);
        // P7: publicar mensaje SolicitudRegistrada (solo si el guardado tuvo exito).
        TempData["Exito"] = $"Solicitud #{solicitud.Id} registrada por {solicitud.MontoSolicitado:C}; está pendiente de evaluación.";
        return RedirectToAction(nameof(Details), new { id = solicitud.Id });
    }

    private async Task<SolicitudCreateViewModel> ArmarCreateVmAsync()
    {
        var userId = users.GetUserId(User)!;
        var cliente = await db.Clientes.FirstOrDefaultAsync(c => c.UsuarioId == userId);

        var vm = new SolicitudCreateViewModel();
        if (cliente is null || !cliente.Activo)
        {
            vm.PuedeRegistrar = false;
            vm.BloqueoMotivo = "Tu cuenta no tiene un cliente activo asociado; no puedes registrar solicitudes.";
            return vm;
        }

        vm.IngresosMensuales = cliente.IngresosMensuales;
        vm.TopeRegistro = SolicitudReglas.MultiploMaximoRegistro * cliente.IngresosMensuales;
        vm.TienePendiente = await db.Solicitudes.AnyAsync(s =>
            s.ClienteId == cliente.Id && s.Estado == EstadoSolicitud.Pendiente);
        vm.PuedeRegistrar = !vm.TienePendiente;
        if (vm.TienePendiente)
            vm.BloqueoMotivo = "Ya tienes una solicitud en estado Pendiente; debes esperar su evaluación.";

        return vm;
    }
}
