namespace TallerMecanico.Aplicacion.Dto;

public sealed class OrdenFormViewModel
{
    public int VehiculoId { get; set; }
    public int MecanicoId { get; set; }
    public DateTime Fecha { get; set; } = DateTime.Today;
    public Guid Token { get; set; }
    public int? ClienteNuevoId { get; set; }
    public List<LineaOrdenFormViewModel> Lineas { get; set; } = [];
}

public sealed class LineaOrdenFormViewModel
{
    public int ProductoId { get; set; }
    public int Cantidad { get; set; }
    public decimal Precio { get; set; }
}

public sealed class LineaOrdenTemporalViewModel
{
    public int ProductoId { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public int Cantidad { get; set; }
    public int Stock { get; set; }
    public decimal Precio { get; set; }
}
