using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace credit_platf.Services;

// Publicador PieSocket v3 via REST (servidor -> nube).
// Room por usuario: "private-user-{UsuarioId}" (PATRON oficial para notificar a un solo usuario).
// Tolera falta de configuracion: devuelve false y solo loguea (no revierte la DB).
public class PieSocketPublisher(IConfiguration config, IHttpClientFactory http, ILogger<PieSocketPublisher> log)
{
    public bool Configurado =>
        !string.IsNullOrWhiteSpace(config["PieSocket:ClusterId"])
        && !string.IsNullOrWhiteSpace(config["PieSocket:ApiKey"])
        && !string.IsNullOrWhiteSpace(config["PieSocket:ApiSecret"]);

    public static string RoomDe(string usuarioId) => $"private-user-{usuarioId}";

    // JWT HS256 para que el navegador se suscriba a SU room (sub = room, user = identidad).
    public string? GenerarJwt(string usuarioId, TimeSpan? vigencia = null)
    {
        var secret = config["PieSocket:ApiSecret"];
        if (string.IsNullOrWhiteSpace(secret)) return null;
        var room = RoomDe(usuarioId);
        var handler = new JwtSecurityTokenHandler();
        var token = handler.CreateToken(new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity([
                new Claim(JwtRegisteredClaimNames.Sub, room),
                new Claim("user", usuarioId),
            ]),
            Expires = DateTime.UtcNow.Add(vigencia ?? TimeSpan.FromHours(1)),
            IssuedAt = DateTime.UtcNow,
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)),
                SecurityAlgorithms.HmacSha256Signature)
        });
        return handler.WriteToken(token);
    }

    public async Task<bool> PublicarAsync(string room, object mensaje)
    {
        if (!Configurado)
        {
            log.LogWarning("PieSocket no configurado; evento no publicado al room {Room}.", room);
            return false;
        }
        try
        {
            var cluster = config["PieSocket:ClusterId"]!;
            var client = http.CreateClient("piesocket");
            var resp = await client.PostAsJsonAsync($"https://{cluster}.piesocket.com/api/publish", new
            {
                key = config["PieSocket:ApiKey"],
                secret = config["PieSocket:ApiSecret"],
                roomId = room,
                message = mensaje
            });
            if (!resp.IsSuccessStatusCode)
                log.LogWarning("PieSocket publish fallo ({Status}) room {Room}.", resp.StatusCode, room);
            return resp.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Error publicando en PieSocket room {Room}.", room);
            return false;
        }
    }
}
