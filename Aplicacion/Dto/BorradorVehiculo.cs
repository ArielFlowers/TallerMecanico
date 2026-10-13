namespace TallerMecanico.Aplicacion.Dto;

public sealed class BorradorVehiculo
{
    public VehiculoFormViewModel Formulario { get; set; } = new();
    public string Modal { get; set; } = "crear";
    public string? Buscar { get; set; }
    public string Usuario { get; set; } = string.Empty;
    public DateTime VenceUtc { get; set; }
    public Guid? OrdenBorradorId { get; set; }
}
