using TallerMecanico.Dominio.Entidades;
using TallerMecanico.Aplicacion.Dto;

namespace TallerMecanico.Aplicacion.Puertos;

public interface IClientePort
{
    IReadOnlyList<Cliente> GetAll();

    Cliente? GetById(int id);

    IReadOnlyList<Cliente> Search(string terminoBusqueda);

    Task<IReadOnlyList<SugerenciaCliente>> SugerirAsync(
        string termino, CancellationToken cancellationToken = default);

    bool ExistsByCi(
        string ci,
        string complementoCi,
        int idExcluido = 0);

    void Add(Cliente cliente);

    void Update(Cliente cliente);

    void Delete(int id);
}
