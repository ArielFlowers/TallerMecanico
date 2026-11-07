using TallerMecanico.Contratos.Catalogos;
using TallerMecanico.Dominio.Entidades;

namespace TallerMecanico.Aplicacion.Puertos;

public interface IConsultaMecanicosPort
{
    List<Mecanico> Search(string terminoBusqueda);

    bool ExistsByCi(string ci, string complementoCi, int idExcluido = 0);

    Task<IReadOnlyList<SugerenciaMecanico>> SugerirAsync(
        string termino, CancellationToken cancellationToken = default, bool incluirEspecialidad = true);
}
