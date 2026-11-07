namespace TallerMecanico.Contratos.Reportes;

public sealed class DocumentoReporteMecanicos
{
    public FiltroReporteMecanicos Filtro { get; set; } = new();

    public IReadOnlyList<FilaReporteMecanico> Filas { get; set; } = [];

    public ResumenReporteMecanicos Resumen { get; set; } = new();

    public DateTime FechaGeneracion { get; set; }

    public string GeneradoPor { get; set; } = string.Empty;
}
