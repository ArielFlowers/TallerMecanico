using TallerMecanico.Aplicacion.DTOs.Reportes;

namespace TallerMecanico.Aplicacion.Puertos;

public interface IGeneradorReporteMecanicosPdfPort
{
    byte[] Generar(DocumentoReporteMecanicos documento);
}
