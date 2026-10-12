using TallerMecanico.Models;

namespace TallerMecanico.Application.Ports;

public interface IDetalleOrdenPort
{
    IReadOnlyList<DetalleOrden> ObtenerPorOrden(int ordenId);
    void Insertar(DetalleOrden detalle);
}
