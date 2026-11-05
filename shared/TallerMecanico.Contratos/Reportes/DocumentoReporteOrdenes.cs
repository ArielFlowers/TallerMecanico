namespace TallerMecanico.Contratos.Reportes;

public sealed class DocumentoReporteOrdenes
{
    public FiltroReporteOrdenes Filtro { get; set; } = new();

    public IReadOnlyList<FilaReporteOrden> Filas { get; set; } = [];

    public ResumenReporteOrdenes Resumen { get; set; } = new();

    public DateTime FechaGeneracion { get; set; }

    public string GeneradoPor { get; set; } = string.Empty;
}
