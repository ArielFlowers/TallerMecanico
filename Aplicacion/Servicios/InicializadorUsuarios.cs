
using TallerMecanico.Application.Ports;
using TallerMecanico.Models;

namespace TallerMecanico.Services;

public sealed class InicializadorUsuarios
{
    private readonly IUsuarioPort _usuarioPort;
    private readonly IPasswordHasher _passwordHasher;

    public InicializadorUsuarios(
        IUsuarioPort usuarioPort,
        IPasswordHasher passwordHasher)
    {
        _usuarioPort = usuarioPort;
        _passwordHasher = passwordHasher;
    }

    public void CrearUsuarioInicial(
        string username,
        string password,
        string rol)
    {
        username = username?.Trim()
            ?? throw new ArgumentNullException(nameof(username));

        ArgumentNullException.ThrowIfNull(password);

        if (username.Length < 3 || username.Length > 50)
        {
            throw new ArgumentException(
                "El usuario debe tener entre 3 y 50 caracteres.");
        }

        if (password.Length < 12 || password.Length > 128)
        {
            throw new ArgumentException(
                "La contraseña debe tener entre 12 y 128 caracteres.");
        }

        if (rol != "Administrador" &&
            rol != "Recepcionista")
        {
            throw new ArgumentException(
                "El rol debe ser Administrador o Recepcionista.");
        }

        if (_usuarioPort.ExisteUsername(username))
        {
            throw new InvalidOperationException(
                "Ya existe una cuenta con ese nombre de usuario.");
        }

        var nuevoUsuario = new Usuario
        {
            Username = username,
            PasswordHash = _passwordHasher.GenerarHash(password),
            Rol = rol,
            Activo = true,
            CreadoPor = "Inicializacion",
            FechaCreacion = DateTime.UtcNow
        };

        _usuarioPort.Agregar(nuevoUsuario);
    }

    public void RestablecerPasswordAdministrador(string password)
    {
        ArgumentNullException.ThrowIfNull(password);

        if (password.Length < 12 || password.Length > 128)
        {
            throw new ArgumentException(
                "La contraseña debe tener entre 12 y 128 caracteres.");
        }

        Usuario usuario = _usuarioPort.ObtenerPorUsername("admin")
            ?? throw new InvalidOperationException(
                "No existe el usuario administrador.");

        if (usuario.Rol != "Administrador" || !usuario.Activo)
        {
            throw new InvalidOperationException(
                "La cuenta admin no es un administrador activo.");
        }

        usuario.PasswordHash = _passwordHasher.GenerarHash(password);
        usuario.ModificadoPor = "RestablecimientoAdministrativo";
        usuario.FechaModificacion = DateTime.UtcNow;

        _usuarioPort.Actualizar(usuario);
    }
}
