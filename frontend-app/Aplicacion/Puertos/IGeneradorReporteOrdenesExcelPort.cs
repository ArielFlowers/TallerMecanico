
namespace TallerMecanico.Aplicacion.Puertos;

public interface IGeneradorReporteOrdenesExcelPort
{
    byte[] Generar(DocumentoReporteOrdenes documento);
}
