namespace TallerMecanico.Aplicacion.Dto;

public sealed class OrdenFormViewModel
{
    public int VehiculoId { get; set; }
    public int MecanicoId { get; set; }
    // ServicioId mantiene compatibilidad con borradores previos.
    public int? ServicioId { get; set; }
    public List<int> ServicioIds { get; set; } = [];
    public Dictionary<int, int> CantidadesServicios { get; set; } = [];
    public Guid Token { get; set; }
    public int? ClienteNuevoId { get; set; }
    public List<LineaOrdenFormViewModel> Lineas { get; set; } = [];
}

public sealed class ServicioSeleccionadoOrdenViewModel
{
    public int Id { get; init; }
    public string Nombre { get; init; } = string.Empty;
    public string Descripcion { get; init; } = string.Empty;
    public decimal Costo { get; init; }
    public decimal TiempoEstimadoHoras { get; init; }
    public string? UnidadCantidad { get; init; }
    public int Cantidad { get; init; } = 1;
    public decimal Subtotal => Costo * Cantidad;
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
