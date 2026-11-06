namespace TallerMecanico.Dominio.Entidades;

public sealed class DetalleOrden
{
    public int Id { get; set; }
    public int OrdenId { get; set; }
    public int ProductoId { get; set; }
    public int Cantidad { get; set; }
    public decimal PrecioUnitario { get; set; }
    public decimal Subtotal { get; set; }

    public decimal CalcularSubtotal() => PrecioUnitario * Cantidad;

    public bool EsValido() => Cantidad > 0 && PrecioUnitario > 0;
}
