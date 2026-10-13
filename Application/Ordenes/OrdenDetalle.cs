using TallerMecanico.Models;

namespace TallerMecanico.Application.Ordenes;

public sealed record OrdenDetalle
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
    public IReadOnlyList<ProductoOrdenDetalle> Productos { get; init; } = [];
}
