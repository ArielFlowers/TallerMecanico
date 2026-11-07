namespace TallerMecanico.Contratos.Ordenes;

public enum EstadoOrden { Activa, Anulada }

public sealed record OrdenResumenDto(
    int Id,
    DateTime Fecha,
    string Placa,
    string? NombreCliente,
    string NombreMecanico,
    string? NombreServicio,
    decimal Total,
    EstadoOrden Estado,
    string CreadoPor);

public sealed record ProductoOrdenDetalleDto(
    string Codigo,
    string Nombre,
    int Cantidad,
    decimal PrecioUnitario,
    decimal Subtotal);

public sealed record ServicioOrdenDetalleDto
{
    public string Nombre { get; init; } = string.Empty;
    public string Descripcion { get; init; } = string.Empty;
    public decimal Costo { get; init; }
    public decimal TiempoEstimadoHoras { get; init; }
    public int Cantidad { get; init; } = 1;
    public string? UnidadCantidad { get; init; }
    public decimal PrecioUnitario { get; init; }
}

public sealed record OrdenDetalleDto
{
    public int Id { get; init; }
    public DateTime Fecha { get; init; }
    public EstadoOrden Estado { get; init; }
    public decimal Total { get; init; }
    public string Placa { get; init; } = string.Empty;
    public string Marca { get; init; } = string.Empty;
    public string Modelo { get; init; } = string.Empty;
    public string? NombreCliente { get; init; }
    public string? CiCliente { get; init; }
    public string? ComplementoCiCliente { get; init; }
    public string? CelularCliente { get; init; }
    public string NombreMecanico { get; init; } = string.Empty;
    public string CiMecanico { get; init; } = string.Empty;
    public string ComplementoCiMecanico { get; init; } = string.Empty;
    public string CreadoPor { get; init; } = string.Empty;
    public DateTime FechaCreacion { get; init; }
    public string? AnuladoPor { get; init; }
    public DateTime? FechaAnulacion { get; init; }
    public ServicioOrdenDetalleDto? Servicio { get; init; }
    public IReadOnlyList<ServicioOrdenDetalleDto> Servicios { get; init; } = [];
    public IReadOnlyList<ProductoOrdenDetalleDto> Productos { get; init; } = [];
}

public sealed record LineaOrdenDto(int ProductoId, int Cantidad);

public sealed record LineaServicioOrdenDto(int ServicioId, int Cantidad);

public sealed record CrearOrdenDto(
    int VehiculoId,
    int MecanicoId,
    DateTime Fecha,
    Guid Token,
    IReadOnlyList<LineaOrdenDto> Lineas,
    int? ServicioId,
    IReadOnlyList<int> ServicioIds,
    IReadOnlyList<LineaServicioOrdenDto> CantidadesServicios);

public sealed record ResultadoOrdenDto(
    bool Exito,
    int? OrdenId,
    decimal Total,
    bool EsReenvio,
    IReadOnlyDictionary<string, string> Errores);
