namespace TallerMecanico.Dominio.Entidades;

public sealed class Orden
{
    public int Id { get; set; }
    public int VehiculoId { get; set; }
    public int MecanicoId { get; set; }
    public int? ServicioId { get; set; }
    public decimal? CostoServicio { get; set; }
    public List<DetalleServicioOrden> Servicios { get; set; } = [];
    public DateTime Fecha { get; set; }
    public EstadoOrden Estado { get; set; } = EstadoOrden.Activa;
    public decimal Total { get; set; }
    public Guid Token { get; set; }
    public string CreadoPor { get; set; } = string.Empty;
    public DateTime FechaCreacion { get; set; }
    public string? AnuladoPor { get; set; }
    public DateTime? FechaAnulacion { get; set; }

    public bool EstaAnulada => Estado == EstadoOrden.Anulada;

    public bool PuedeAnularse() => !EstaAnulada;

    public void Anular(string usuario, DateTime fechaUtc)
    {
        if (!PuedeAnularse())
            throw new InvalidOperationException("La orden ya está anulada.");

        Estado = EstadoOrden.Anulada;
        AnuladoPor = usuario;
        FechaAnulacion = fechaUtc;
    }

    public decimal CalcularTotal(IEnumerable<DetalleOrden> repuestos) =>
        repuestos.Sum(repuesto => repuesto.Subtotal) + Servicios.Sum(servicio => servicio.Costo);
}
