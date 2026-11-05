using TallerMecanico.Aplicacion.DTOs.Reportes;

namespace TallerMecanico.Aplicacion.Puertos;

public interface IGeneradorReporteOrdenesPdfPort
{
    byte[] Generar(DocumentoReporteOrdenes documento);
}
