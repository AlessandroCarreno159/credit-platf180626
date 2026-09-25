using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using credit_platf.Models;

namespace credit_platf.Services;

// Item plano para cachear el listado (sin entidades EF: evita ciclos de navegacion).
public class SolicitudCacheItem
{
    public int Id { get; set; }
    public decimal MontoSolicitado { get; set; }
    public DateTime FechaSolicitud { get; set; }
    public EstadoSolicitud Estado { get; set; }

    public static SolicitudCacheItem Desde(SolicitudCredito s) => new()
    {
        Id = s.Id,
        MontoSolicitado = s.MontoSolicitado,
        FechaSolicitud = s.FechaSolicitud,
        Estado = s.Estado
    };
}

// Cache Redis del listado "Mis solicitudes": 60 s por usuario+filtros.
// Invalida con InvalidarUsuarioAsync al registrar (P3) o cambiar estado (P5).
public class CacheSolicitudes(IDistributedCache cache)
{
    private static readonly TimeSpan Duracion = TimeSpan.FromSeconds(60);
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public static string ClaveListado(string userId, SolicitudFiltros f)
    {
        var plano = $"{f.Estado}|{f.MontoMin}|{f.MontoMax}|{f.Desde:O}|{f.Hasta:O}";
        var hash = Convert.ToHexString(SHA1.HashData(Encoding.UTF8.GetBytes(plano)))[..12];
        return $"solicitudes:{userId}:{hash}";
    }

    public static string ClaveIndice(string userId) => $"solicitudes:keys:{userId}";

    public async Task<List<SolicitudCacheItem>?> LeerAsync(string clave)
    {
        var json = await cache.GetStringAsync(clave);
        return json is null ? null : JsonSerializer.Deserialize<List<SolicitudCacheItem>>(json, JsonOpts);
    }

    public async Task GuardarAsync(string userId, string clave, List<SolicitudCacheItem> items)
    {
        var opts = new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = Duracion };
        await cache.SetStringAsync(clave, JsonSerializer.Serialize(items, JsonOpts), opts);
        await AgregarAIndiceAsync(userId, clave);
    }

    public async Task InvalidarUsuarioAsync(string userId)
    {
        var indice = await cache.GetStringAsync(ClaveIndice(userId));
        if (indice is null) return;
        var claves = JsonSerializer.Deserialize<List<string>>(indice, JsonOpts) ?? new();
        foreach (var c in claves.Distinct())
            await cache.RemoveAsync(c);
        await cache.RemoveAsync(ClaveIndice(userId));
    }

    private async Task AgregarAIndiceAsync(string userId, string clave)
    {
        var indiceKey = ClaveIndice(userId);
        var json = await cache.GetStringAsync(indiceKey);
        var claves = json is null ? new List<string>() : JsonSerializer.Deserialize<List<string>>(json, JsonOpts) ?? new();
        if (!claves.Contains(clave))
        {
            claves.Add(clave);
            await cache.SetStringAsync(indiceKey,
                JsonSerializer.Serialize(claves, JsonOpts),
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = Duracion });
        }
    }
}
