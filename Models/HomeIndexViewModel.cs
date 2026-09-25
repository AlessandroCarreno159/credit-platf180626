using credit_platf.Models;

namespace credit_platf.Models;

public class SolicitudResumen
{
    public int Id { get; set; }
    public string ClienteEmail { get; set; } = string.Empty;
    public decimal IngresosMensuales { get; set; }
    public decimal MontoSolicitado { get; set; }
    public DateTime FechaSolicitud { get; set; }
    public EstadoSolicitud Estado { get; set; }
    public string? MotivoRechazo { get; set; }

    // Calculado en servidor con SolicitudReglas (P1 visible).
    public bool EsAprobable { get; set; }
    public decimal TopeAprobacion { get; set; }
}

public class HomeIndexViewModel
{
    public int TotalClientes { get; set; }
    public int TotalPendientes { get; set; }
    public int TotalAprobadas { get; set; }
    public int TotalRechazadas { get; set; }
    public List<SolicitudResumen> Ultimas { get; set; } = new();
}
