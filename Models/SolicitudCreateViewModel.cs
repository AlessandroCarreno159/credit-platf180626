using System.ComponentModel.DataAnnotations;

namespace credit_platf.Models;

// Formulario de registro de solicitud (P3). El POST solo bindea MontoSolicitado;
// ingresos, tope y estado de Pendiente se recalculan siempre en el servidor.
public class SolicitudCreateViewModel
{
    [Required(ErrorMessage = "El campo {0} es obligatorio.")]
    [Range(0.01, double.MaxValue, ErrorMessage = "El monto solicitado debe ser mayor a 0.")]
    [DataType(DataType.Currency)]
    [Display(Name = "Monto solicitado")]
    public decimal MontoSolicitado { get; set; }

    // Solo informativos (no bindeados en el POST).
    public decimal IngresosMensuales { get; set; }
    public decimal TopeRegistro { get; set; }
    public bool TienePendiente { get; set; }
    public bool PuedeRegistrar { get; set; }
    public string? BloqueoMotivo { get; set; }
}
