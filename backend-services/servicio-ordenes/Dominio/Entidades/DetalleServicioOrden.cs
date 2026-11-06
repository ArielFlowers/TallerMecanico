namespace TallerMecanico.Dominio.Entidades;

public sealed class DetalleServicioOrden
{
    public int OrdenId { get; set; }
    public int ServicioId { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
    public decimal Costo { get; set; }
    public decimal TiempoEstimadoHoras { get; set; }
    public int Cantidad { get; set; } = 1;
    public string? UnidadCantidad { get; set; }
}
