namespace TallerMecanico.Contratos.Reportes;

public sealed class FilaReporteMecanico
{
    public int MecanicoId { get; set; }

    public string NombreMecanico { get; set; } = string.Empty;

    public string CiMecanico { get; set; } = string.Empty;

    public int TotalOrdenes { get; set; }

    public decimal TotalFacturado { get; set; }

    public decimal PromedioPorOrden { get; set; }
}
