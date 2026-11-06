using TallerMecanico.Dominio.Entidades;

namespace TallerMecanico.Aplicacion.CasosUso.Ordenes.Contratos;

public interface IProductoStockRepository
{
    IReadOnlyList<Producto> BloquearPorId(IEnumerable<int> ids);
    bool Descontar(int productoId, int cantidad);
    bool Restituir(int productoId, int cantidad);
}
