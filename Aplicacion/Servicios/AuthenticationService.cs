
using TallerMecanico.Aplicacion.Puertos;
using TallerMecanico.Dominio.Entidades;

namespace TallerMecanico.Aplicacion.Servicios;

public sealed class AuthenticationService : IAuthenticationService
{
    private readonly IUsuarioPort _usuarioPort;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IFallbackHash _fallbackHash;

    public AuthenticationService(
        IUsuarioPort usuarioPort,
        IPasswordHasher passwordHasher,
        IFallbackHash fallbackHash)
    {
        _usuarioPort = usuarioPort;
        _passwordHasher = passwordHasher;
        _fallbackHash = fallbackHash;
    }

    public UsuarioAutenticado? ValidarCredenciales(
        string username,
        string password)
    {
        if (string.IsNullOrWhiteSpace(username) ||
            string.IsNullOrEmpty(password))
        {
            return null;
        }

        Usuario? usuario = _usuarioPort.ObtenerPorUsername(
            username.Trim());

        bool usuarioDisponible =
            usuario is not null && usuario.Activo;

        string hashAVerificar = usuarioDisponible
            ? usuario!.PasswordHash
            : _fallbackHash.Hash;

        bool passwordValido =
            _passwordHasher.VerificarPassword(
                password,
                hashAVerificar);

        if (!usuarioDisponible || !passwordValido)
        {
            return null;
        }

        return new UsuarioAutenticado(
            usuario!.Id,
            usuario.Username,
            usuario.Rol);
    }
}
