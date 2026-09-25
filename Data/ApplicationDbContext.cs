using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using credit_platf.Models;

namespace credit_platf.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : IdentityDbContext(options)
{
    public DbSet<Cliente> Clientes => Set<Cliente>();
    public DbSet<SolicitudCredito> Solicitudes => Set<SolicitudCredito>();
    public DbSet<Notificacion> Notificaciones => Set<Notificacion>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Cliente: un registro por usuario de Identity.
        builder.Entity<Cliente>(e =>
        {
            e.HasIndex(c => c.UsuarioId).IsUnique();
            e.Property(c => c.IngresosMensuales).HasColumnType("decimal(18,2)");
            e.ToTable(t => t.HasCheckConstraint("CK_Cliente_Ingresos", "IngresosMensuales > 0"));
            e.HasOne(c => c.Usuario)
                .WithMany()
                .HasForeignKey(c => c.UsuarioId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Solicitud: montos > 0 y una sola Pendiente por cliente (indice parcial).
        // La regla Monto <= 5x ingresos (aprobacion) y <= 10x (registro) se valida
        // en servidor (P3/P5) porque requiere join con Cliente.
        builder.Entity<SolicitudCredito>(e =>
        {
            e.Property(s => s.MontoSolicitado).HasColumnType("decimal(18,2)");
            e.Property(s => s.MotivoRechazo).HasMaxLength(500);
            e.ToTable(t => t.HasCheckConstraint("CK_Solicitud_Monto", "MontoSolicitado > 0"));
            e.HasIndex(s => s.ClienteId)
                .IsUnique()
                .HasFilter("[Estado] = 0");
            e.HasOne(s => s.Cliente)
                .WithMany(c => c.Solicitudes)
                .HasForeignKey(s => s.ClienteId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Notificacion (P7): MessageId unico => redelivery no duplica.
        builder.Entity<Notificacion>(e =>
        {
            e.HasIndex(n => n.MessageId).IsUnique();
            e.Property(n => n.Texto).HasMaxLength(500);
            e.HasOne(n => n.Solicitud)
                .WithMany()
                .HasForeignKey(n => n.SolicitudId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
