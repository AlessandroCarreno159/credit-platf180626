using System.ComponentModel.DataAnnotations;

namespace credit_platf.Models;

// Filtros del catalogo "Mis solicitudes" (P2). Llegan por query string (GET).
// La validacion es 100% servidor via Validar(); los atributos HTML (min, type=date)
// son solo ayuda visual, nunca garantia.
public class SolicitudFiltros
{
    [Display(Name = "Estado")]
    public EstadoSolicitud? Estado { get; set; }

    [Display(Name = "Monto mínimo")]
    [DataType(DataType.Currency)]
    public decimal? MontoMin { get; set; }

    [Display(Name = "Monto máximo")]
    [DataType(DataType.Currency)]
    public decimal? MontoMax { get; set; }

    [Display(Name = "Desde")]
    [DataType(DataType.Date)]
    public DateTime? Desde { get; set; }

    [Display(Name = "Hasta")]
    [DataType(DataType.Date)]
    public DateTime? Hasta { get; set; }

    public IEnumerable<string> Validar()
    {
        if (MontoMin is < 0 || MontoMax is < 0)
            yield return "Los montos no pueden ser negativos.";
        if (MontoMin.HasValue && MontoMax.HasValue && MontoMin > MontoMax)
            yield return "El monto mínimo no puede superar al monto máximo.";
        if (Desde.HasValue && Hasta.HasValue && Desde.Value.Date > Hasta.Value.Date)
            yield return "La fecha de inicio no puede ser posterior a la fecha fin.";
    }

    public bool TieneAlgunFiltro =>
        Estado.HasValue || MontoMin.HasValue || MontoMax.HasValue || Desde.HasValue || Hasta.HasValue;
}

public class MisSolicitudesViewModel
{
    public SolicitudFiltros Filtros { get; set; } = new();
    public List<SolicitudCredito> Resultados { get; set; } = new();
    public bool TieneCliente { get; set; }
}
