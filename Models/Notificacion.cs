using System.ComponentModel.DataAnnotations;

namespace credit_platf.Models;

// Creada en P1 para que P7 (CloudMQ) solo agregue el consumidor.
// No aprueba ni rechaza creditos, solo guarda la notificacion de recepcion.
public class Notificacion
{
    public int Id { get; set; }

    [Required]
    public Guid MessageId { get; set; }

    [Required]
    public int SolicitudId { get; set; }

    public SolicitudCredito? Solicitud { get; set; }

    [Required]
    public string UsuarioId { get; set; } = string.Empty;

    [Required]
    [MaxLength(500)]
    public string Texto { get; set; } = string.Empty;

    public DateTime FechaProcesamientoUtc { get; set; } = DateTime.UtcNow;
}
