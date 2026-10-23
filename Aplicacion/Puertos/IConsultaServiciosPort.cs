using TallerMecanico.Aplicacion.Dto;
using TallerMecanico.Dominio.Entidades;

namespace TallerMecanico.Aplicacion.Puertos;

public interface IConsultaServiciosPort
{
    List<Servicio> ObtenerCatalogo();
    Servicio? ObtenerDelCatalogo(int id);
    Task<IReadOnlyList<SugerenciaServicio>> SugerirCatalogoAsync(
        string termino, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Servicio>> BuscarAsync(
        string termino, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SugerenciaServicio>> SugerirAsync(
        string termino, CancellationToken cancellationToken = default);
}
