using TallerMecanico.Dominio.Entidades;
using TallerMecanico.Aplicacion.Dto;

namespace TallerMecanico.Aplicacion.Puertos;

public interface IProductoPort
{
    IReadOnlyList<Producto> GetAll();

    Producto? GetById(
        int id);

    IReadOnlyList<Producto> Search(
        string terminoBusqueda);

    Task<IReadOnlyList<SugerenciaProducto>> SugerirAsync(
        string termino, CancellationToken cancellationToken = default);

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
