
using TallerMecanico.Models;

namespace TallerMecanico.Application.Ports;

public interface IUsuarioPort
{
    Usuario? ObtenerPorUsername(string username);

    Usuario? ObtenerPorId(int id);

    bool ExisteUsername(string username);

    bool ExisteEmail(string email);

    void Agregar(Usuario usuario);

    void Actualizar(Usuario usuario);
}
