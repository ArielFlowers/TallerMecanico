
namespace TallerMecanico.Aplicacion.Puertos;

// Puerto de entrada: Reporte 1, lista ordenada de órdenes de trabajo.
public interface IReporteOrdenesPort
{
    DocumentoReporteOrdenes ObtenerDatos(FiltroReporteOrdenes filtro, string generadoPor);

    byte[] GenerarPdf(FiltroReporteOrdenes filtro, string generadoPor);

    byte[] GenerarExcel(FiltroReporteOrdenes filtro, string generadoPor);
}
