using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using credit_platf.Data;
using credit_platf.Models;
using credit_platf.Services;

namespace credit_platf.Controllers;

public class HomeController(ApplicationDbContext db) : Controller
{
    public async Task<IActionResult> Index()
    {
        var solicitudes = await db.Solicitudes
            .Include(s => s.Cliente)
            .ThenInclude(c => c!.Usuario)
            .OrderByDescending(s => s.FechaSolicitud)
            .Take(5)
            .ToListAsync();

        var vm = new HomeIndexViewModel
        {
            TotalClientes = await db.Clientes.CountAsync(),
            TotalPendientes = await db.Solicitudes.CountAsync(s => s.Estado == EstadoSolicitud.Pendiente),
            TotalAprobadas = await db.Solicitudes.CountAsync(s => s.Estado == EstadoSolicitud.Aprobado),
            TotalRechazadas = await db.Solicitudes.CountAsync(s => s.Estado == EstadoSolicitud.Rechazado),
            Ultimas = solicitudes.Select(s => new SolicitudResumen
            {
                Id = s.Id,
                ClienteEmail = s.Cliente?.Usuario?.Email ?? "(sin usuario)",
                IngresosMensuales = s.Cliente?.IngresosMensuales ?? 0,
                MontoSolicitado = s.MontoSolicitado,
                FechaSolicitud = s.FechaSolicitud,
                Estado = s.Estado,
                MotivoRechazo = s.MotivoRechazo,
                EsAprobable = SolicitudReglas.EsAprobable(
                    s.MontoSolicitado, s.Cliente?.IngresosMensuales ?? 0),
                TopeAprobacion = SolicitudReglas.MultiploMaximoAprobacion *
                    (s.Cliente?.IngresosMensuales ?? 0)
            }).ToList()
        };

        return View(vm);
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
