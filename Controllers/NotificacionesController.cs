using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using credit_platf.Data;

namespace credit_platf.Controllers;

// Notificaciones de recepcion generadas por el consumidor MQ (P7).
// Solo lectura, filtrada por el usuario autenticado. No aprueba ni rechaza.
[Authorize]
public class NotificacionesController(ApplicationDbContext db, UserManager<IdentityUser> users) : Controller
{
    public async Task<IActionResult> Index()
    {
        var userId = users.GetUserId(User)!;
        var lista = await db.Notificaciones
            .Where(n => n.UsuarioId == userId)
            .OrderByDescending(n => n.FechaProcesamientoUtc)
            .ToListAsync();
        return View(lista);
    }
}
