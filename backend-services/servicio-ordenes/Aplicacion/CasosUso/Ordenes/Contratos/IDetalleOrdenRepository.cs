using TallerMecanico.Dominio.Entidades;

namespace TallerMecanico.Aplicacion.CasosUso.Ordenes.Contratos;

public interface IDetalleOrdenRepository
{
    IReadOnlyList<DetalleOrden> ObtenerPorOrden(int ordenId);
    void Insertar(DetalleOrden detalle);
}
