using TallerMecanico.Dominio.Entidades;

namespace TallerMecanico.Aplicacion.Puertos;

public interface IDetalleOrdenPort
{
    IReadOnlyList<DetalleOrden> ObtenerPorOrden(int ordenId);
    void Insertar(DetalleOrden detalle);
}
