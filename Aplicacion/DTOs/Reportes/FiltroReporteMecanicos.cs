namespace TallerMecanico.Aplicacion.DTOs.Reportes;

public sealed class FiltroReporteMecanicos
{
    public DateOnly? FechaDesde { get; set; }

    public DateOnly? FechaHasta { get; set; }

    public int? MecanicoId { get; set; }
}
