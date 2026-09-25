using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using credit_platf.Services;

namespace credit_platf.Controllers;

// Configuracion de tiempo real para el navegador (P6).
// La identidad sale del servidor (claims); el cliente nunca elige room ni usuario.
[Authorize]
[Route("api/realtime")]
public class RealtimeController(UserManager<IdentityUser> users, PieSocketPublisher pie, IConfiguration config) : ControllerBase
{
    // GET /api/realtime/token -> { clusterId, apiKey, room, jwt } del PROPIO usuario.
    // Sin secrets configurados responde 503 para que el JS use solo SignalR.
    [HttpGet("token")]
    public IActionResult Token()
    {
        var userId = users.GetUserId(User)!;
        var jwt = pie.GenerarJwt(userId);
        if (jwt is null || !pie.Configurado)
            return StatusCode(503, new { error = "PieSocket no configurado en el servidor." });
        return Ok(new
        {
            clusterId = config["PieSocket:ClusterId"],
            apiKey = config["PieSocket:ApiKey"],
            room = PieSocketPublisher.RoomDe(userId),
            jwt
        });
    }
}
