
using TallerMecanico.Dominio.Entidades;

namespace TallerMecanico.Aplicacion.Puertos;

public interface IUsuarioPort
{
    Usuario? ObtenerPorUsername(string username);

    Usuario? ObtenerPorId(int id);

    bool ExisteUsername(string username);

    bool ExisteEmail(string email);

    void Agregar(Usuario usuario);

    void Actualizar(Usuario usuario);
}
