namespace TallerMecanico.Aplicacion.DTOs.Reportes;

public sealed class FiltroReporteMecanicos
{
    public string? NombreMecanico { get; set; }

    public DateOnly? FechaDesde { get; set; }

    public DateOnly? FechaHasta { get; set; }

    public int? MecanicoId { get; set; }
}
