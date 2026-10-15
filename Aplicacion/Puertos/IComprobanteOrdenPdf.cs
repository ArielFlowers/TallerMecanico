using TallerMecanico.Aplicacion.CasosUso.Ordenes;

namespace TallerMecanico.Aplicacion.Puertos;

public interface IComprobanteOrdenPdf
{
    byte[] Generar(OrdenDetalle orden, string totalLiteral, string generadoPor, DateTimeOffset fechaGeneracion);
}
