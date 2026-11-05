
namespace TallerMecanico.Aplicacion.Puertos;

public interface IGeneradorReporteOrdenesPdfPort
{
    byte[] Generar(DocumentoReporteOrdenes documento);
}
