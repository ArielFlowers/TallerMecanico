using TallerMecanico.Models;

namespace TallerMecanico.Application.Ports;

public interface IProductoPort
{
    IReadOnlyList<Producto> GetAll();

    Producto? GetById(
        int id);

    IReadOnlyList<Producto> Search(
        string terminoBusqueda);

    bool ExistsByCodigo(
        string codigo,
        int idExcluido = 0);

    void Add(
        Producto producto);

    void Update(
        Producto producto);

    void Delete(
        int id);

    bool Descontar(
        int productoId,
        int cantidad);

    void Restituir(
        int productoId,
        int cantidad);
}