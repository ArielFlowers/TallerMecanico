using System.Security.Cryptography;
using TallerMecanico.Aplicacion.Errores;
using TallerMecanico.Aplicacion.Puertos;
using TallerMecanico.Dominio.Entidades;
using TallerMecanico.Contratos.Usuarios;

namespace TallerMecanico.Aplicacion.Servicios;

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
        if (!ReglasCuenta.UsuarioValido(username))
        {
            throw new ArgumentException(
                "El nombre de usuario no es válido.", nameof(username));
        }

        if (!ReglasCuenta.EmailValido(email))
        {
            throw new ArgumentException(
                "El correo electrónico no es válido.", nameof(email));
        }

        if (!ReglasCuenta.ContrasenaValida(password))
        {
            throw new ArgumentException(
                "La contraseña no cumple los requisitos.", nameof(password));
        }

        string usuarioNormalizado = username.Trim();
        string emailNormalizado = email.Trim().ToLowerInvariant();

        if (_usuarios.ExisteUsername(usuarioNormalizado) ||
            _solicitudes.ExisteUsername(usuarioNormalizado))
        {
            throw new SolicitudRegistroDuplicadaException(nameof(SolicitudRegistro.Username));
        }

        if (_usuarios.ExisteEmail(emailNormalizado) ||
            _solicitudes.ExisteEmail(emailNormalizado))
        {
            throw new SolicitudRegistroDuplicadaException(nameof(SolicitudRegistro.Email));
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
