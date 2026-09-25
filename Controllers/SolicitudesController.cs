using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using credit_platf.Data;
using credit_platf.Models;

namespace credit_platf.Controllers;

// Catalogo de solicitudes del usuario autenticado (P2).
// El UsuarioId siempre sale del servidor (claims); nunca de la URL ni del form.
[Authorize]
public class SolicitudesController(ApplicationDbContext db, UserManager<IdentityUser> users) : Controller
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

        return View(solicitud);
    }
}
