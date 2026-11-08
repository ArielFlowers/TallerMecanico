using System.Security.Cryptography;
using TallerMecanico.Aplicacion.Errores;
using TallerMecanico.Aplicacion.Puertos;
using TallerMecanico.Dominio.Entidades;
using TallerMecanico.Contratos.Usuarios;
using TallerMecanico.Contratos;

namespace TallerMecanico.Aplicacion.Servicios;

public sealed class UsuarioAdministracionService : IGestionUsuariosPort
{
    private readonly IUsuarioAdministracionPort _usuarios;
    private readonly ISolicitudRegistroPort _solicitudes;
    private readonly IEmailService _email;
    private readonly IConfiguration _configuration;
    private readonly IAuditoriaPort _auditoria;
    private readonly ICurrentUser _usuarioActual;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IActivacionInvitacionPort _activacionInvitacion;
    private readonly IGeneradorNombreUsuarioPort _generador;

    public UsuarioAdministracionService(
        IUsuarioAdministracionPort usuarios,
        ISolicitudRegistroPort solicitudes,
        IEmailService email,
        IConfiguration configuration,
        IAuditoriaPort auditoria,
        ICurrentUser usuarioActual,
        IPasswordHasher passwordHasher,
        IActivacionInvitacionPort activacionInvitacion,
        IGeneradorNombreUsuarioPort generador)
    {
        _usuarios = usuarios;
        _solicitudes = solicitudes;
        _email = email;
        _configuration = configuration;
        _auditoria = auditoria;
        _usuarioActual = usuarioActual;
        _passwordHasher = passwordHasher;
        _activacionInvitacion = activacionInvitacion;
        _generador = generador;
    }

    public string CrearInvitacion(
        string username,
        string email,
        string rol,
        string password,
        CancellationToken cancellationToken = default)
    {
        ValidarAdministrador();

        if (!ReglasCuenta.EmailValido(email))
        {
            throw new ArgumentException(
                "El correo electrónico no es válido.", nameof(email));
        }

        if (rol != Roles.Administrador && rol != Roles.Recepcionista)
        {
            throw new ArgumentException(
                "El rol debe ser Administrador o Recepcionista.", nameof(rol));
        }

        if (!ReglasCuenta.ContrasenaValida(password))
        {
            throw new ArgumentException(
                ReglasCuenta.MensajeContrasena,
                nameof(password));
        }

        string emailNormalizado = email.Trim().ToLowerInvariant();

        string usuarioFinal;
        if (string.IsNullOrWhiteSpace(username))
        {
            // Protocolo de generación de nombre de usuario a partir del correo.
            usuarioFinal = _generador.GenerarUsuarioUnico(
                emailNormalizado, cancellationToken);
        }
        else
        {
            string usuarioNormalizado = username.Trim();

            if (!ReglasCuenta.UsuarioValido(usuarioNormalizado))
            {
                throw new ArgumentException(
                    "El nombre de usuario no es válido.", nameof(username));
            }

            usuarioFinal = usuarioNormalizado;
        }

        if (_usuarios.ExisteUsername(usuarioFinal) ||
            _solicitudes.ExisteUsername(usuarioFinal))
        {
            throw new SolicitudRegistroDuplicadaException(nameof(SolicitudRegistro.Username));
        }

        if (_usuarios.ExisteEmail(emailNormalizado) ||
            _solicitudes.ExisteEmail(emailNormalizado))
        {
            throw new SolicitudRegistroDuplicadaException(nameof(SolicitudRegistro.Email));
        }

        byte[] tokenBytes = RandomNumberGenerator.GetBytes(32);
        string token = Convert.ToHexString(tokenBytes);
        string tokenHash = Convert.ToHexString(SHA256.HashData(tokenBytes));

        DateTime ahora = DateTime.UtcNow;

        var solicitud = new SolicitudRegistro
        {
            Username = usuarioFinal,
            Email = emailNormalizado,
            PasswordHash = _passwordHasher.GenerarHash(password),
            Estado = "PendienteVerificacion",
            TokenVerificacionHash = tokenHash,
            TokenExpiraEn = ahora.AddHours(1),
            FechaSolicitud = ahora,
            UltimoIntentoVerificacionEn = ahora,
            Rol = rol,
            CreadoPor = _usuarioActual.Username
        };

        _solicitudes.Agregar(solicitud);

        Uri uriBase = EnlaceVerificacion.ObtenerBase(_configuration);
        _email.ValidarConfiguracion();
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            _email.EnviarVerificacionAsync(
                emailNormalizado,
                EnlaceVerificacion.CrearActivacionInvitacion(uriBase, token),
                cancellationToken,
                usuarioFinal).GetAwaiter().GetResult();
        }
        catch (Exception ex) when (
            ex is not OperationCanceledException ||
            !cancellationToken.IsCancellationRequested)
        {
            throw new SolicitudGuardadaCorreoPendienteException(ex);
        }

        _auditoria.Registrar(new RegistroAuditoria
        {
            UsuarioId = _usuarioActual.UsuarioId,
            Username = _usuarioActual.Username,
            Accion = "USUARIO_INVITADO",
            Entidad = "Usuario",
            EntidadId = usuarioFinal,
            Fecha = DateTime.UtcNow,
            Detalle = $"Rol: {rol}"
        });

        return token;
    }

    public bool ActivarUsuarioDesdeInvitacion(
        string token,
        out string? errorMessage)
    {
        errorMessage = null;

        if (string.IsNullOrWhiteSpace(token) || token.Length != 64)
        {
            errorMessage = "Token inválido.";
            return false;
        }

        byte[] tokenBytes;

        try
        {
            tokenBytes = Convert.FromHexString(token);
        }
        catch (FormatException)
        {
            errorMessage = "Token inválido.";
            return false;
        }

        string tokenHash = Convert.ToHexString(
            SHA256.HashData(tokenBytes));

        var resultado = _activacionInvitacion.ActivarPorTokenHash(tokenHash);

        if (!resultado.Exito)
        {
            errorMessage = resultado.MensajeError;
            return false;
        }

        _auditoria.Registrar(new RegistroAuditoria
        {
            UsuarioId = null,
            Username = resultado.Username,
            Accion = "USUARIO_ACTIVADO",
            Entidad = "Usuario",
            EntidadId = resultado.Username!,
            Fecha = DateTime.UtcNow,
            Detalle = $"Rol asignado: {resultado.Rol}"
        });

        return true;
    }
    public bool ActualizarUsuario(
        int id,
        string email,
        string rol,
        bool activo,
        out string? errorMessage)
    {
        ValidarAdministrador();

        errorMessage = null;

        if (!ReglasCuenta.EmailValido(email))
        {
            errorMessage = "El correo electrónico no es válido.";
            return false;
        }

        if (rol != Roles.Administrador && rol != Roles.Recepcionista)
        {
            errorMessage = "El rol debe ser Administrador o Recepcionista.";
            return false;
        }

        Usuario? usuario = _usuarios.ObtenerPorIdConCredenciales(id);

        if (usuario is null)
        {
            errorMessage = "Usuario no encontrado.";
            return false;
        }

        int administradorId = _usuarioActual.UsuarioId ?? 0;

        if (usuario.Id == administradorId)
        {
            if (!activo)
            {
                errorMessage = "No puede desactivar su propia cuenta.";
                return false;
            }

            if (rol != Roles.Administrador)
            {
                errorMessage = "No puede cambiar su propio rol.";
                return false;
            }
        }

        string emailNormalizado = email.Trim().ToLowerInvariant();

        if (!string.Equals(usuario.Email, emailNormalizado, StringComparison.OrdinalIgnoreCase))
        {
            if (_usuarios.ExisteEmail(emailNormalizado) || _solicitudes.ExisteEmail(emailNormalizado))
            {
                errorMessage = "El correo electrónico ya está en uso.";
                return false;
            }
        }

        bool resultado = _usuarios.ActualizarUsuarioAtomico(
            id,
            emailNormalizado,
            rol,
            activo,
            _usuarioActual.Username ?? "sistema",
            out string error);

        if (!resultado)
        {
            errorMessage = error;
            return false;
        }

        _auditoria.Registrar(new RegistroAuditoria
        {
            UsuarioId = administradorId,
            Username = _usuarioActual.Username,
            Accion = "USUARIO_ACTUALIZADO",
            Entidad = "Usuario",
            EntidadId = id.ToString(),
            Fecha = DateTime.UtcNow,
            Detalle = $"Email: {emailNormalizado}, Rol: {rol}, Activo: {activo}"
        });

        return true;
    }

    public bool DesactivarUsuario(int id, out string? errorMessage)
    {
        ValidarAdministrador();

        errorMessage = null;

        Usuario? usuario = _usuarios.ObtenerPorIdConCredenciales(id);

        if (usuario is null)
        {
            errorMessage = "Usuario no encontrado.";
            return false;
        }

        int administradorId = _usuarioActual.UsuarioId ?? 0;

        if (usuario.Id == administradorId)
        {
            errorMessage = "No puede desactivar su propia cuenta.";
            return false;
        }

        bool resultado = _usuarios.DesactivarUsuarioAtomico(id, _usuarioActual.Username ?? "sistema", out string error);

        if (!resultado)
        {
            errorMessage = error;
            return false;
        }

        _auditoria.Registrar(new RegistroAuditoria
        {
            UsuarioId = administradorId,
            Username = _usuarioActual.Username,
            Accion = "USUARIO_DESACTIVADO",
            Entidad = "Usuario",
            EntidadId = id.ToString(),
            Fecha = DateTime.UtcNow,
            Detalle = $"Rol: {usuario.Rol}"
        });

        return true;
    }

    public bool ReactivarUsuario(int id, out string? errorMessage)
    {
        ValidarAdministrador();

        errorMessage = null;

        Usuario? usuario = _usuarios.ObtenerPorIdConCredenciales(id);

        if (usuario is null)
        {
            errorMessage = "Usuario no encontrado.";
            return false;
        }

        if (usuario.Activo)
        {
            errorMessage = "El usuario ya está activo.";
            return false;
        }

        usuario.Activo = true;
        usuario.ModificadoPor = _usuarioActual.Username ?? "sistema";
        usuario.FechaModificacion = DateTime.UtcNow;

        _usuarios.Actualizar(usuario);

        _auditoria.Registrar(new RegistroAuditoria
        {
            UsuarioId = _usuarioActual.UsuarioId,
            Username = _usuarioActual.Username,
            Accion = "USUARIO_REACTIVADO",
            Entidad = "Usuario",
            EntidadId = usuario.Id.ToString(),
            Fecha = DateTime.UtcNow,
            Detalle = $"Rol: {usuario.Rol}"
        });

        return true;
    }

    private int ValidarAdministrador()
    {
        if (!_usuarioActual.EstaAutenticado ||
            _usuarioActual.Rol != Roles.Administrador ||
            _usuarioActual.UsuarioId is not int administradorId ||
            administradorId <= 0)
        {
            throw new UnauthorizedAccessException(
                "Solo un administrador puede gestionar usuarios.");
        }

        return administradorId;
    }

}