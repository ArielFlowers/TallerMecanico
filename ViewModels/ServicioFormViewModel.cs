using System.ComponentModel.DataAnnotations;

namespace TallerMecanico.ViewModels;

public class ServicioFormViewModel
{
    [Required(ErrorMessage = "El nombre del servicio es obligatorio.")]
    [StringLength(50,
        ErrorMessage = "El nombre no puede superar los 50 caracteres.")]
    public string Nombre { get; set; } = string.Empty;

    [Required(ErrorMessage = "La descripción del servicio es obligatoria.")]
    [StringLength(150,
        ErrorMessage = "La descripción no puede superar los 150 caracteres.")]
    public string Descripcion { get; set; } = string.Empty;

    [Range(0.01, double.MaxValue,
        ErrorMessage = "El costo debe ser mayor a cero.")]
    public decimal Costo { get; set; }

    [Range(0.01, double.MaxValue,
        ErrorMessage = "El tiempo estimado debe ser mayor a cero.")]
    public decimal TiempoEstimadoHoras { get; set; }
}