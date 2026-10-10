
using TallerMecanico.Models;

namespace TallerMecanico.Application.Ports;

public interface IAuthenticationService
{
    UsuarioAutenticado? ValidarCredenciales(
        string username,
        string password);
}
