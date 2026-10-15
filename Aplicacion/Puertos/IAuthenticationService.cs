
using TallerMecanico.Dominio.Entidades;

namespace TallerMecanico.Aplicacion.Puertos;

public interface IAuthenticationService
{
    UsuarioAutenticado? ValidarCredenciales(
        string username,
        string password);
}
