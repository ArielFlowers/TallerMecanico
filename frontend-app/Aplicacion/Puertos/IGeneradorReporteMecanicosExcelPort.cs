
namespace TallerMecanico.Aplicacion.Puertos;

public interface IGeneradorReporteMecanicosExcelPort
{
    byte[] Generar(DocumentoReporteMecanicos documento);
}
