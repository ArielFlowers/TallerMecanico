namespace TallerMecanico.Aplicacion.DTOs.Reportes;

public sealed class ResumenReporteOrdenes
{
    public int TotalOrdenes { get; set; }

    public decimal TotalGeneral { get; set; }

    public int OrdenesActivas { get; set; }

    public int OrdenesAnuladas { get; set; }
}
