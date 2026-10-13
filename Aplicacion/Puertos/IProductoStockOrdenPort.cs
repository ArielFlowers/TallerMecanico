using TallerMecanico.Models;

namespace TallerMecanico.Application.Ports;

public interface IProductoStockOrdenPort
{
    IReadOnlyList<Producto> BloquearPorId(IEnumerable<int> ids);
    bool Descontar(int productoId, int cantidad);
    bool Restituir(int productoId, int cantidad);
}
