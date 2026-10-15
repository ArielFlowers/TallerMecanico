namespace TallerMecanico.Aplicacion.CasosUso.Ordenes;

public sealed record ServicioOrdenDetalle
{
    public string Nombre { get; init; } = string.Empty;
    public string Descripcion { get; init; } = string.Empty;
    public decimal Costo { get; init; }
    public decimal TiempoEstimadoHoras { get; init; }
}
