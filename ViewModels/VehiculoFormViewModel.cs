using System.ComponentModel.DataAnnotations;

namespace TallerMecanico.ViewModels;

public class VehiculoFormViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "La placa es obligatoria.")]
    [RegularExpression("^[A-Za-z0-9]{6,8}$",
        ErrorMessage = "Formato alfanumérico requerido.")]
    public string Placa { get; set; } = string.Empty;

    [Required(ErrorMessage = "El modelo es obligatorio.")]
    [StringLength(60,
        ErrorMessage = "El modelo no puede superar los 60 caracteres.")]
    public string Modelo { get; set; } = string.Empty;

    [Range(0, int.MaxValue,
        ErrorMessage = "El kilometraje no puede ser negativo.")]
    public int Kilometraje { get; set; }

    [StringLength(250,
        ErrorMessage = "Las observaciones no pueden superar los 250 caracteres.")]
    public string? Observaciones { get; set; }
}
