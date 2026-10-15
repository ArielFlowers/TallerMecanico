namespace TallerMecanico.Dominio.Entidades;

public class Servicio
{
    public int Id { get; set; }

    public string Nombre { get; set; } = string.Empty;

    public string Descripcion { get; set; } = string.Empty;

    public decimal Costo { get; set; }

    public decimal TiempoEstimadoHoras { get; set; }

    public string CreadoPor { get; set; } = string.Empty;

    public DateTime FechaCreacion { get; set; }

    public string? ModificadoPor { get; set; }

    public DateTime? FechaModificacion { get; set; }
}