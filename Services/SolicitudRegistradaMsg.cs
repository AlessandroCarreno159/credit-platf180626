namespace credit_platf.Services;

// Contrato del mensaje SolicitudRegistrada (P7). JSON persistente en la cola.
public class SolicitudRegistradaMsg
{
    public Guid MessageId { get; set; }
    public int SolicitudId { get; set; }
    public string UsuarioId { get; set; } = string.Empty;
    public DateTime FechaEventoUtc { get; set; }
}
