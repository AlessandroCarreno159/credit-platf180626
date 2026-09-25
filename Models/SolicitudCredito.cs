using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace credit_platf.Models;

public class SolicitudCredito
{
    public int Id { get; set; }

    [Required]
    [Display(Name = "Cliente")]
    public int ClienteId { get; set; }

    [ForeignKey(nameof(ClienteId))]
    public Cliente? Cliente { get; set; }

    [Required]
    [Range(0.01, double.MaxValue, ErrorMessage = "El monto solicitado debe ser mayor a 0.")]
    [Column(TypeName = "decimal(18,2)")]
    [Display(Name = "Monto solicitado")]
    public decimal MontoSolicitado { get; set; }

    [Display(Name = "Fecha de solicitud")]
    public DateTime FechaSolicitud { get; set; } = DateTime.UtcNow;

    [Display(Name = "Estado")]
    public EstadoSolicitud Estado { get; set; } = EstadoSolicitud.Pendiente;

    [MaxLength(500)]
    [Display(Name = "Motivo de rechazo")]
    public string? MotivoRechazo { get; set; }
}
