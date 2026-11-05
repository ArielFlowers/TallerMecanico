using TallerMecanico.Aplicacion.DTOs.Reportes;

namespace TallerMecanico.Aplicacion.Puertos;

public interface IConsultaReportesPort
{
    DocumentoReporteOrdenes ObtenerReporteOrdenes(
        FiltroReporteOrdenes filtro,
        string generadoPor);

    DocumentoReporteMecanicos ObtenerReporteMecanicos(
        FiltroReporteMecanicos filtro,
        string generadoPor);

    IReadOnlyList<MecanicoListaDto> ObtenerMecanicos();
}
