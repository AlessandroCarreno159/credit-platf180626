using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using credit_platf.Data;
using credit_platf.Services;

namespace credit_platf.Controllers;

// Reenvio manual MQ solo-Development (P7): republica con el MISMO MessageId.
// El consumidor lo ignora si ya fue procesado (idempotencia). No existe en Production.
[Authorize]
[Route("api/mq")]
public class MqDevController(
    IWebHostEnvironment env,
    ApplicationDbContext db,
    UserManager<IdentityUser> users,
    RabbitMqPublisher mq) : ControllerBase
{
    public record ReenvioDto(int SolicitudId, Guid MessageId);

    [HttpPost("reenviar")]
    public async Task<IActionResult> Reenviar([FromBody] ReenvioDto dto)
    {
        if (!env.IsDevelopment())
            return NotFound();
        if (dto.MessageId == Guid.Empty || dto.SolicitudId <= 0)
            return BadRequest("MessageId y SolicitudId son obligatorios.");
        var userId = users.GetUserId(User)!;
        var solicitud = await db.Solicitudes.FindAsync(dto.SolicitudId);
        if (solicitud is null)
            return NotFound();
        var cliente = await db.Clientes.FindAsync(solicitud.ClienteId);
        if (cliente?.UsuarioId != userId && !User.IsInRole("Analista"))
            return Forbid();
        var (ok, id) = await mq.PublicarConIdAsync(dto.MessageId, dto.SolicitudId, cliente!.UsuarioId);
        return Ok(new { ok, messageId = id });
    }
}
