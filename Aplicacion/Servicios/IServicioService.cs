using TallerMecanico.Dominio.Entidades;
using TallerMecanico.Aplicacion.Dto;

namespace TallerMecanico.Aplicacion.Servicios;

public interface IServicioService
{
    List<Servicio> ObtenerCatalogo();
    Servicio? ObtenerDelCatalogo(int id);
    Task<IReadOnlyList<SugerenciaServicio>> SugerirCatalogoAsync(
        string? termino, CancellationToken cancellationToken = default);

    List<Servicio> ObtenerTodos();
    Servicio? ObtenerPorId(int id);
    Task<IReadOnlyList<Servicio>> BuscarAsync(
        string? termino, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SugerenciaServicio>> SugerirAsync(
        string? termino, CancellationToken cancellationToken = default);
    void Crear(Servicio servicio);
    void Actualizar(Servicio servicio);
    void Eliminar(int id);
}
