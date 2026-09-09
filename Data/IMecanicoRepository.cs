using TallerMecanico.Models;

namespace TallerMecanico.Data;

public interface IMecanicoRepository
{
    Task<int> CrearAsync(Mecanico mecanico);

    Task<IReadOnlyList<Mecanico>> ObtenerAsync(
        string? terminoBusqueda = null);

    Task<bool> ActualizarAsync(Mecanico mecanico);

    Task<bool> EliminarAsync(int id);

    Task<bool> ExisteCiAsync(
        string ci,
        int? idExcluido = null);
}
