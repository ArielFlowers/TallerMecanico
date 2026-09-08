using System.ComponentModel.DataAnnotations;

namespace TallerMecanico.ViewModels;

public class ServicioFormViewModel
{
    [Required(ErrorMessage = "El nombre del servicio es obligatorio.")]
    public string Nombre { get; set; } = string.Empty;

    [Required(ErrorMessage = "La descripción del servicio es obligatoria.")]
    public string Descripcion { get; set; } = string.Empty;

    [Range(0.01, double.MaxValue,
        ErrorMessage = "El costo debe ser mayor a cero.")]
    public decimal Costo { get; set; }

    [Range(0.01, double.MaxValue,
        ErrorMessage = "El tiempo estimado debe ser mayor a cero.")]
    public decimal TiempoEstimadoHoras { get; set; }
}
