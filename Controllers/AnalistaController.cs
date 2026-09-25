using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using credit_platf.Data;
using credit_platf.Models;
using credit_platf.Services;

namespace credit_platf.Controllers;

// Panel de evaluacion de riesgo (P5). Solo rol Analista (el rol se crea en el seed P1).
[Authorize(Roles = "Analista")]
public class AnalistaController(ApplicationDbContext db, CacheSolicitudes cacheSol) : Controller
{
    [Route("/Analista")]
    [Route("/Analista/Index")]
    public async Task<IActionResult> Index()
    {
        var pendientes = await db.Solicitudes
            .Include(s => s.Cliente)
            .ThenInclude(c => c!.Usuario)
            .Where(s => s.Estado == EstadoSolicitud.Pendiente)
            .OrderBy(s => s.FechaSolicitud)
            .ToListAsync();

        return View(pendientes.Select(s => new AnalistaFila
        {
            Solicitud = s,
            EsAprobable = SolicitudReglas.EsAprobable(
                s.MontoSolicitado, s.Cliente?.IngresosMensuales ?? 0),
            TopeAprobacion = SolicitudReglas.MultiploMaximoAprobacion *
                (s.Cliente?.IngresosMensuales ?? 0)
        }).ToList());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Route("/Analista/Aprobar")]
    public async Task<IActionResult> Aprobar(int id)
    {
        var solicitud = await db.Solicitudes
            .Include(s => s.Cliente)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (solicitud is null || solicitud.Estado != EstadoSolicitud.Pendiente)
        {
            TempData["Error"] = "La solicitud ya fue procesada; no se puede aprobar.";
            return RedirectToAction(nameof(Index));
        }

        if (!SolicitudReglas.EsAprobable(solicitud.MontoSolicitado, solicitud.Cliente?.IngresosMensuales ?? 0))
        {
            TempData["Error"] = $"No se puede aprobar: el monto {solicitud.MontoSolicitado:C} supera 5 veces los ingresos ({SolicitudReglas.MultiploMaximoAprobacion * (solicitud.Cliente?.IngresosMensuales ?? 0):C}).";
            return RedirectToAction(nameof(Index));
        }

        solicitud.Estado = EstadoSolicitud.Aprobado;
        await db.SaveChangesAsync();
        await cacheSol.InvalidarUsuarioAsync(solicitud.Cliente!.UsuarioId);
        // P6: emitir evento SolicitudEstadoActualizado al propietario.
        TempData["Exito"] = $"Solicitud #{solicitud.Id} aprobada.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Route("/Analista/Rechazar")]
    public async Task<IActionResult> Rechazar(int id, string? motivo)
    {
        if (string.IsNullOrWhiteSpace(motivo))
        {
            TempData["Error"] = "El motivo de rechazo es obligatorio.";
            return RedirectToAction(nameof(Index));
        }

        var solicitud = await db.Solicitudes
            .Include(s => s.Cliente)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (solicitud is null || solicitud.Estado != EstadoSolicitud.Pendiente)
        {
            TempData["Error"] = "La solicitud ya fue procesada; no se puede rechazar.";
            return RedirectToAction(nameof(Index));
        }

        solicitud.Estado = EstadoSolicitud.Rechazado;
        solicitud.MotivoRechazo = motivo.Trim();
        await db.SaveChangesAsync();
        await cacheSol.InvalidarUsuarioAsync(solicitud.Cliente!.UsuarioId);
        // P6: emitir evento SolicitudEstadoActualizado al propietario.
        TempData["Exito"] = $"Solicitud #{solicitud.Id} rechazada.";
        return RedirectToAction(nameof(Index));
    }
}

public class AnalistaFila
{
    public SolicitudCredito Solicitud { get; set; } = null!;
    public bool EsAprobable { get; set; }
    public decimal TopeAprobacion { get; set; }
}
