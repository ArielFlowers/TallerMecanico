namespace TallerMecanico.ViewModels;

public class ProductoFormViewModel
{
    public int Id { get; set; }

    public string? Codigo { get; set; }

    public string? Nombre { get; set; }

    public decimal? Precio { get; set; }

    public int? Stock { get; set; }

    public int? StockMinimo { get; set; }
}