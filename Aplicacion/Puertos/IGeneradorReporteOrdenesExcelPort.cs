using TallerMecanico.Aplicacion.DTOs.Reportes;

namespace TallerMecanico.Aplicacion.Puertos;

public interface IGeneradorReporteOrdenesExcelPort
{
    byte[] Generar(DocumentoReporteOrdenes documento);
}
