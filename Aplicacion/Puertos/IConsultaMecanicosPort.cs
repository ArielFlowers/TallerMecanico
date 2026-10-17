using TallerMecanico.Aplicacion.Dto;

namespace TallerMecanico.Aplicacion.Puertos;

public interface IConsultaMecanicosPort
{
    Task<IReadOnlyList<SugerenciaMecanico>> SugerirAsync(
        string termino, CancellationToken cancellationToken = default);
}
