using System.Data.Common;
using System.Security.Cryptography;
using TallerMecanico.Aplicacion.Errores;
using TallerMecanico.Aplicacion.Puertos;
using TallerMecanico.Dominio.Entidades;
using TallerMecanico.Aplicacion.Validaciones;
using TallerMecanico.Infraestructura.Persistencia;

namespace TallerMecanico.Aplicacion.Servicios;

public sealed class UsuarioAdministracionService
{
    private readonly IUsuarioAdministracionPort _usuarios;
    private readonly ISolicitudRegistroPort _solicitudes;
    private readonly IEmailService _email;
    private readonly IConfiguration _configuration;
    private readonly IAuditoriaPort _auditoria;
    private readonly ICurrentUser _usuarioActual;
    private readonly IPasswordHasher _passwordHasher;
    private readonly DatabaseConnectionFactory _connectionFactory;

    public UsuarioAdministracionService(
        IUsuarioAdministracionPort usuarios,
        ISolicitudRegistroPort solicitudes,
        IEmailService email,
        IConfiguration configuration,
        IAuditoriaPort auditoria,
        ICurrentUser usuarioActual,
        IPasswordHasher passwordHasher,
        DatabaseConnectionFactory connectionFactory)
    {
        _usuarios = usuarios;
        _solicitudes = solicitudes;
        _email = email;
        _configuration = configuration;
        _auditoria = auditoria;
        _usuarioActual = usuarioActual;
        _passwordHasher = passwordHasher;
        _connectionFactory = connectionFactory;
    }

    public string CrearInvitacion(
        string username,
        string email,
        string rol,
        CancellationToken cancellationToken = default)
    {
        ValidarAdministrador();

        if (!ValidadorSolicitudRegistro.UsuarioValido(username))
        {
            throw new ArgumentException(
                "El nombre de usuario no es válido.", nameof(username));
        }

        if (!ValidadorSolicitudRegistro.EmailValido(email))
        {
            throw new ArgumentException(
                "El correo electrónico no es válido.", nameof(email));
        }

        if (rol != "Administrador" && rol != "Recepcionista")
        {
            throw new ArgumentException(
                "El rol debe ser Administrador o Recepcionista.", nameof(rol));
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

        byte[] tokenBytes = RandomNumberGenerator.GetBytes(32);
        string token = Convert.ToHexString(tokenBytes);
        string tokenHash = Convert.ToHexString(SHA256.HashData(tokenBytes));

        DateTime ahora = DateTime.UtcNow;

        var solicitud = new SolicitudRegistro
        {
            Username = usuarioNormalizado,
            Email = emailNormalizado,
            PasswordHash = string.Empty,
            Estado = "PendienteVerificacion",
            TokenVerificacionHash = tokenHash,
            TokenExpiraEn = ahora.AddHours(1),
            FechaSolicitud = ahora,
            UltimoIntentoVerificacionEn = ahora,
            Rol = rol
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
                cancellationToken).GetAwaiter().GetResult();
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
            EntidadId = usuarioNormalizado,
            Fecha = DateTime.UtcNow,
            Detalle = $"Rol: {rol}"
        });

        return token;
    }

    public bool ActivarUsuarioDesdeInvitacion(
        string token,
        string password,
        out string? errorMessage)
    {
        errorMessage = null;

        if (string.IsNullOrWhiteSpace(token) || token.Length != 64)
        {
            errorMessage = "Token inválido.";
            return false;
        }

        if (!ValidadorSolicitudRegistro.PasswordValido(password))
        {
            errorMessage = "La contraseña no cumple los requisitos mínimos (12-128 caracteres).";
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

        string tokenHash = Convert.ToHexString(SHA256.HashData(tokenBytes));

        var solicitud = _solicitudes.ConfirmarEmail(tokenHash);

        if (solicitud is null || solicitud.Estado != "PendienteAprobacion")
        {
            errorMessage = "El enlace de invitación no es válido o ha expirado.";
            return false;
        }

        string passwordHash = _passwordHasher.GenerarHash(password);

        using DbConnection connection = _connectionFactory.CreateConnection();
        connection.Open();

        using DbTransaction transaction = connection.BeginTransaction();

        try
        {
            using DbCommand insertar = connection.CreateCommand();
            insertar.Transaction = transaction;

            string rolUsuario = solicitud.Rol ?? "Recepcionista";

            insertar.CommandText = """
                INSERT INTO Usuarios
                (
                    Username,
                    PasswordHash,
                    Email,
                    EmailVerificado,
                    Rol,
                    Activo,
                    CreadoPor,
                    FechaCreacion
                )
                VALUES
                (
                    @Username,
                    @PasswordHash,
                    @Email,
                    1,
                    @Rol,
                    1,
                    @CreadoPor,
                    UTC_TIMESTAMP()
                );
                """;

            AddParameter(insertar, "@Username", solicitud.Username);
            AddParameter(insertar, "@PasswordHash", passwordHash);
            AddParameter(insertar, "@Email", solicitud.Email);
            AddParameter(insertar, "@Rol", rolUsuario);
            AddParameter(insertar, "@CreadoPor", "sistema");

            if (insertar.ExecuteNonQuery() != 1)
            {
                transaction.Rollback();
                errorMessage = "No se pudo crear el usuario.";
                return false;
            }

            using DbCommand actualizar = connection.CreateCommand();
            actualizar.Transaction = transaction;

            actualizar.CommandText = """
                UPDATE SolicitudesRegistro
                SET Estado = 'Aprobada',
                    FechaResolucion = UTC_TIMESTAMP(6),
                    RevisadoPorUsuarioId = NULL
                WHERE Id = @SolicitudId
                  AND Estado = 'PendienteAprobacion'
                  AND EmailVerificadoEn IS NOT NULL;
                """;

            AddParameter(actualizar, "@SolicitudId", solicitud.Id);

            if (actualizar.ExecuteNonQuery() != 1)
            {
                transaction.Rollback();
                errorMessage = "No se pudo completar la activación.";
                return false;
            }

            transaction.Commit();

            _auditoria.Registrar(new RegistroAuditoria
            {
                UsuarioId = null,
                Username = solicitud.Username,
                Accion = "USUARIO_ACTIVADO",
                Entidad = "Usuario",
                EntidadId = solicitud.Username,
                Fecha = DateTime.UtcNow,
                Detalle = $"Rol asignado: {rolUsuario}"
            });

            return true;
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
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

        if (!ValidadorSolicitudRegistro.EmailValido(email))
        {
            errorMessage = "El correo electrónico no es válido.";
            return false;
        }

        if (rol != "Administrador" && rol != "Recepcionista")
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

            if (rol != "Administrador")
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
            _usuarioActual.Rol != "Administrador" ||
            _usuarioActual.UsuarioId is not int administradorId ||
            administradorId <= 0)
        {
            throw new UnauthorizedAccessException(
                "Solo un administrador puede gestionar usuarios.");
        }

        return administradorId;
    }

    private static void AddParameter(
        DbCommand command,
        string nombre,
        object? valor)
    {
        DbParameter parametro = command.CreateParameter();
        parametro.ParameterName = nombre;
        parametro.Value = valor ?? DBNull.Value;
        command.Parameters.Add(parametro);
    }
}