using TallerMecanico.Application.Ordenes;

namespace TallerMecanico.Application.Ports;

public interface IComprobanteOrdenPdf
{
    byte[] Generar(OrdenDetalle orden, string totalLiteral, string generadoPor, DateTimeOffset fechaGeneracion);
}
