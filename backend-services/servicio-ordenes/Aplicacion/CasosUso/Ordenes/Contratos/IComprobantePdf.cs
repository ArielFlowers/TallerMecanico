using TallerMecanico.Aplicacion.CasosUso.Ordenes;

namespace TallerMecanico.Aplicacion.CasosUso.Ordenes.Contratos;

public interface IComprobantePdf
{
    byte[] Generar(OrdenDetalle orden, string totalLiteral, string generadoPor, DateTimeOffset fechaGeneracion);
}
