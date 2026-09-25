using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace credit_platf.Hubs;

// Hub de notificaciones de solicitudes (P6). Protegido con Identity: anonimo => 401.
// El destinatario es siempre Clients.User(usuarioId) resuelto en el servidor;
// el navegador nunca elige a quien va el evento.
[Authorize]
public class SolicitudesHub : Hub
{
    // Estado vigente de una solicitud (para recuperar cambios tras una reconexion).
    // La verificacion de propietario/Analista la hace el endpoint EstadoJson;
    // aqui solo se expone el mecanismo de grupo por si se usa a futuro.
    public string GrupoPropio() => $"user-{Context.UserIdentifier}";
}
