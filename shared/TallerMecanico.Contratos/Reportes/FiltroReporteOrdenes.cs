namespace TallerMecanico.Contratos.Reportes;

public sealed class FiltroReporteOrdenes
{
    public DateOnly? FechaDesde { get; set; }

    public DateOnly? FechaHasta { get; set; }

    public string? Estado { get; set; }
}
