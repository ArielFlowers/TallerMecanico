using System.Security.Cryptography;
using TallerMecanico.Application.Ports;
using TallerMecanico.Models;
using TallerMecanico.Validators;

namespace TallerMecanico.Services;

public sealed class RegistroUsuarioService
{
    private readonly IUsuarioPort _usuarios;
    private readonly ISolicitudRegistroPort _solicitudes;
    private readonly IPasswordHasher _passwordHasher;

    public RegistroUsuarioService(
        IUsuarioPort usuarios,
        ISolicitudRegistroPort solicitudes,
        IPasswordHasher passwordHasher)
    {
        _usuarios = usuarios;
        _solicitudes = solicitudes;
        _passwordHasher = passwordHasher;
    }

    // El token devuelto es exclusivamente para el futuro
    // servicio de correo. Nunca se debe mostrar al usuario
    // ni guardar en logs.
    public string CrearSolicitudPendiente(
        string username,
        string email,
        string password)
    {
        if (!ValidadorSolicitudRegistro.UsuarioValido(username))
        {
            throw new ArgumentException(
                "El nombre de usuario no es valido.");
        }

        if (!ValidadorSolicitudRegistro.EmailValido(email))
        {
            throw new ArgumentException(
                "El correo electronico no es valido.");
        }

        if (!ValidadorSolicitudRegistro.PasswordValido(password))
        {
            throw new ArgumentException(
                "La contrasena no cumple los requisitos.");
        }

        string usuarioNormalizado = username.Trim();
        string emailNormalizado = email.Trim().ToLowerInvariant();

        if (_usuarios.ExisteUsername(usuarioNormalizado) ||
            _solicitudes.ExisteUsername(usuarioNormalizado))
        {
            throw new InvalidOperationException(
                "El nombre de usuario no esta disponible.");
        }

        if (_usuarios.ExisteEmail(emailNormalizado) ||
            _solicitudes.ExisteEmail(emailNormalizado))
        {
            throw new InvalidOperationException(
                "El correo electronico no esta disponible.");
        }

        string passwordHash =
            _passwordHasher.GenerarHash(password);

        byte[] tokenBytes = RandomNumberGenerator.GetBytes(32);

        string token = Convert.ToHexString(tokenBytes);

        string tokenHash = Convert.ToHexString(
            SHA256.HashData(tokenBytes));

        DateTime ahora = DateTime.UtcNow;

        var solicitud = new SolicitudRegistro
        {
            Username = usuarioNormalizado,
            Email = emailNormalizado,
            PasswordHash = passwordHash,
            Estado = "PendienteVerificacion",
            TokenVerificacionHash = tokenHash,
            TokenExpiraEn = ahora.AddHours(1),
            FechaSolicitud = ahora,
            UltimoIntentoVerificacionEn = ahora
        };

        _solicitudes.Agregar(solicitud);

        return token;
    }
}