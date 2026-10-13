namespace TallerMecanico.Models;

public class Servicio
{
    public int Id { get; set; }

    public string Nombre { get; set; } = string.Empty;

    public string Descripcion { get; set; } = string.Empty;

    public decimal Costo { get; set; }

    public decimal TiempoEstimadoHoras { get; set; }
}