namespace TallerMecanico.Models;

public class Producto
{
    public int Id { get; set; }

    public string Codigo { get; set; } = string.Empty;

    public string Nombre { get; set; } = string.Empty;

    public decimal Precio { get; set; }

    public int Stock { get; set; }

    public int StockMinimo { get; set; }

    public string CreadoPor { get; set; } = string.Empty;

    public DateTime FechaCreacion { get; set; }
}