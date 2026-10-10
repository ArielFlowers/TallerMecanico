namespace TallerMecanico.Models;

public sealed class Orden
{
    public int Id { get; set; }
    public int VehiculoId { get; set; }
    public int MecanicoId { get; set; }
    public DateTime Fecha { get; set; }
    public EstadoOrden Estado { get; set; } = EstadoOrden.Activa;
    public decimal Total { get; set; }
    public Guid Token { get; set; }
    public string CreadoPor { get; set; } = string.Empty;
    public DateTime FechaCreacion { get; set; }
    public string? AnuladoPor { get; set; }
    public DateTime? FechaAnulacion { get; set; }
}
