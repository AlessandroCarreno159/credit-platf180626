using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using credit_platf.Models;

namespace credit_platf.Data;

public static class SeedData
{
    public const string RolAnalista = "Analista";
    public const string EmailAnalista = "analista@platf.test";
    public const string EmailCliente1 = "cliente1@platf.test";
    public const string EmailCliente2 = "cliente2@platf.test";
    public const string EmailPiloto = "piloto@platf.test";
    public const string PasswordSeed = "Clave123!";

    public static async Task InitializeAsync(IServiceProvider services)
    {
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = services.GetRequiredService<UserManager<IdentityUser>>();
        var db = services.GetRequiredService<ApplicationDbContext>();

        if (!await roleManager.RoleExistsAsync(RolAnalista))
            await roleManager.CreateAsync(new IdentityRole(RolAnalista));

        var analista = await EnsureUserAsync(userManager, EmailAnalista);
        if (!await userManager.IsInRoleAsync(analista, RolAnalista))
            await userManager.AddToRoleAsync(analista, RolAnalista);

        var u1 = await EnsureUserAsync(userManager, EmailCliente1);
        var u2 = await EnsureUserAsync(userManager, EmailCliente2);
        var up = await EnsureUserAsync(userManager, EmailPiloto);

        var c1 = await db.Clientes.FirstOrDefaultAsync(c => c.UsuarioId == u1.Id)
            ?? (await AddClienteAsync(db, u1.Id, 2500m));
        var c2 = await db.Clientes.FirstOrDefaultAsync(c => c.UsuarioId == u2.Id)
            ?? (await AddClienteAsync(db, u2.Id, 4000m));

        if (!await db.Solicitudes.AnyAsync(s => s.ClienteId == c1.Id))
        {
            db.Solicitudes.Add(new SolicitudCredito
            {
                ClienteId = c1.Id,
                MontoSolicitado = 5000m,
                FechaSolicitud = DateTime.UtcNow,
                Estado = EstadoSolicitud.Pendiente
            });
        }

        if (!await db.Solicitudes.AnyAsync(s => s.ClienteId == c2.Id))
        {
            db.Solicitudes.Add(new SolicitudCredito
            {
                ClienteId = c2.Id,
                MontoSolicitado = 8000m,
                FechaSolicitud = DateTime.UtcNow.AddDays(-2),
                Estado = EstadoSolicitud.Aprobado
            });
        }

        // Piloto P2: un cliente con 3 solicitudes en distintos estados, montos y
        // fechas para evidenciar los filtros (estado, rango de monto, rango de fechas).
        // Solo una Pendiente (respeta el indice parcial).
        var cp = await db.Clientes.FirstOrDefaultAsync(c => c.UsuarioId == up.Id)
            ?? (await AddClienteAsync(db, up.Id, 3000m));

        var ahora = DateTime.UtcNow;
        var seedPiloto = new[]
        {
            new SolicitudCredito { ClienteId = cp.Id, MontoSolicitado = 4000m, FechaSolicitud = ahora, Estado = EstadoSolicitud.Pendiente },
            new SolicitudCredito { ClienteId = cp.Id, MontoSolicitado = 9000m, FechaSolicitud = ahora.AddDays(-10), Estado = EstadoSolicitud.Aprobado },
            new SolicitudCredito
            {
                ClienteId = cp.Id,
                MontoSolicitado = 20000m,
                FechaSolicitud = ahora.AddDays(-20),
                Estado = EstadoSolicitud.Rechazado,
                MotivoRechazo = "Monto superior a 5 veces los ingresos."
            },
        };

        foreach (var s in seedPiloto)
        {
            var existe = await db.Solicitudes.AnyAsync(x =>
                x.ClienteId == s.ClienteId && x.MontoSolicitado == s.MontoSolicitado && x.Estado == s.Estado);
            if (!existe)
                db.Solicitudes.Add(s);
        }

        await db.SaveChangesAsync();
    }

    private static async Task<IdentityUser> EnsureUserAsync(UserManager<IdentityUser> users, string email)
    {
        var user = await users.FindByEmailAsync(email);
        if (user is not null) return user;
        user = new IdentityUser { UserName = email, Email = email, EmailConfirmed = true };
        var result = await users.CreateAsync(user, PasswordSeed);
        if (!result.Succeeded)
            throw new InvalidOperationException($"Seed usuario {email}: " +
                string.Join("; ", result.Errors.Select(e => e.Description)));
        return user;
    }

    private static async Task<Cliente> AddClienteAsync(ApplicationDbContext db, string usuarioId, decimal ingresos)
    {
        var cliente = new Cliente { UsuarioId = usuarioId, IngresosMensuales = ingresos, Activo = true };
        db.Clientes.Add(cliente);
        await db.SaveChangesAsync();
        return cliente;
    }
}
