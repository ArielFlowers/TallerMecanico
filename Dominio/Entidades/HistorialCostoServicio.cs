namespace TallerMecanico.Models;

public class HistorialCostoServicio
{
    public int Id { get; set; }

    public int ServicioId { get; set; }

    public string NombreServicio { get; set; } = string.Empty;

    public decimal CostoAnterior { get; set; }

    public decimal CostoNuevo { get; set; }

    public DateTime FechaCambio { get; set; }
}