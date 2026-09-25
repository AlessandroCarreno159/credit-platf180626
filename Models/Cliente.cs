using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Identity;

namespace credit_platf.Models;

public class Cliente
{
    public int Id { get; set; }

    [Required]
    public string UsuarioId { get; set; } = string.Empty;

    [ForeignKey(nameof(UsuarioId))]
    public IdentityUser? Usuario { get; set; }

    [Required]
    [Range(0.01, double.MaxValue, ErrorMessage = "Los ingresos mensuales deben ser mayores a 0.")]
    [Column(TypeName = "decimal(18,2)")]
    [Display(Name = "Ingresos mensuales")]
    public decimal IngresosMensuales { get; set; }

    [Display(Name = "Activo")]
    public bool Activo { get; set; } = true;

    public ICollection<SolicitudCredito> Solicitudes { get; set; } = new List<SolicitudCredito>();
}
