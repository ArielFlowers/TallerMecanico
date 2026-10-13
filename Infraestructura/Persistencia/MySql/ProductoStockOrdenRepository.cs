using System.Data.Common;
using TallerMecanico.Application.Ports;
using TallerMecanico.Models;

namespace TallerMecanico.Infraestructura.Adapters.MySql;

public sealed class ProductoStockOrdenRepository(
    ProductoRepository repositorio, DbConnection conexion, DbTransaction transaccion) : IProductoStockOrdenPort
{
    public IReadOnlyList<Producto> BloquearPorId(IEnumerable<int> ids) => RepositorioOrdenSql.Ejecutar(() =>
    {
        var productos = new List<Producto>();
        foreach (int id in ids.Distinct().Order())
        {
            Producto? producto = repositorio.ObtenerParaActualizar(id, conexion, transaccion);
            if (producto is not null) productos.Add(producto);
        }
        return productos;
    });

    public bool Descontar(int productoId, int cantidad) => RepositorioOrdenSql.Ejecutar(() =>
        repositorio.Descontar(productoId, cantidad, conexion, transaccion));

    public bool Restituir(int productoId, int cantidad) => RepositorioOrdenSql.Ejecutar(() =>
        repositorio.Restituir(productoId, cantidad, conexion, transaccion));
}
