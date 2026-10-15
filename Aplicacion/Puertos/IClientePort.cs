using TallerMecanico.Dominio.Entidades;

namespace TallerMecanico.Aplicacion.Puertos;

public interface IClientePort
{
    IReadOnlyList<Cliente> GetAll();

    Cliente? GetById(int id);

    IReadOnlyList<Cliente> Search(string terminoBusqueda);

    bool ExistsByCi(
        string ci,
        string complementoCi,
        int idExcluido = 0);

    void Add(Cliente cliente);

    void Update(Cliente cliente);

    void Delete(int id);
}