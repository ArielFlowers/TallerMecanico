
namespace TallerMecanico.Aplicacion.Puertos;

// Puerto de entrada: Reporte 2, información sumariada por mecánico con gráfico.
public interface IReporteMecanicosPort
{
    DocumentoReporteMecanicos ObtenerDatos(FiltroReporteMecanicos filtro, string generadoPor);

    byte[] GenerarPdf(FiltroReporteMecanicos filtro, string generadoPor);

    byte[] GenerarExcel(FiltroReporteMecanicos filtro, string generadoPor);
}
