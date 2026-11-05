namespace TallerMecanico.Aplicacion.CasosUso.Ordenes;

public sealed record ProductoOrdenDetalle(
    string Codigo,
    string Nombre,
    int Cantidad,
    decimal PrecioUnitario,
    decimal Subtotal);
