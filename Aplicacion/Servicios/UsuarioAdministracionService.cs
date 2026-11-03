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
    private readonly IGeneradorNombreUsuarioPort _generador;

    public UsuarioAdministracionService(
        IUsuarioAdministracionPort usuarios,
        ISolicitudRegistroPort solicitudes,
        IEmailService email,
        IConfiguration configuration,
        IAuditoriaPort auditoria,
        ICurrentUser usuarioActual,
        IPasswordHasher passwordHasher,
        DatabaseConnectionFactory connectionFactory,
        IGeneradorNombreUsuarioPort generador)
    {
        _usuarios = usuarios;
        _solicitudes = solicitudes;
        _email = email;
        _configuration = configuration;
        _auditoria = auditoria;
        _usuarioActual = usuarioActual;
        _passwordHasher = passwordHasher;
        _connectionFactory = connectionFactory;
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

        if (!ValidadorSolicitudRegistro.PasswordValido(password))
        {
            throw new ArgumentException(
                "La contraseña debe tener entre 12 y 128 caracteres y no comenzar ni terminar con espacios.",
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

            if (!ValidadorSolicitudRegistro.UsuarioValido(usuarioNormalizado))
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

        using DbConnection connection =
            _connectionFactory.CreateConnection();

        connection.Open();

        using DbTransaction transaction =
            connection.BeginTransaction();

        string username;
        string email;
        string passwordHash;
        string rolUsuario;
        long solicitudId;

        try
        {
            // Bloquear exclusivamente la solicitud asociada al token.
            using (DbCommand buscar = connection.CreateCommand())
            {
                buscar.Transaction = transaction;

                buscar.CommandText = """
                    SELECT Id, Username, Email, PasswordHash, Rol
                    FROM SolicitudesRegistro
                    WHERE TokenVerificacionHash = @TokenHash
                      AND Estado = 'PendienteVerificacion'
                      AND EmailVerificadoEn IS NULL
                      AND TokenExpiraEn > UTC_TIMESTAMP(6)
                    LIMIT 1
                    FOR UPDATE;
                    """;

                AddParameter(buscar, "@TokenHash", tokenHash);

                using DbDataReader reader = buscar.ExecuteReader();

                if (!reader.Read())
                {
                    transaction.Rollback();
                    errorMessage =
                        "El enlace de invitación no es válido o ha expirado.";
                    return false;
                }

                solicitudId = reader.GetInt64(
                    reader.GetOrdinal("Id"));

                username = reader.GetString(
                    reader.GetOrdinal("Username"));

                email = reader.GetString(
                    reader.GetOrdinal("Email"));

                passwordHash = reader.GetString(
                    reader.GetOrdinal("PasswordHash"));

                int indiceRol = reader.GetOrdinal("Rol");

                rolUsuario = reader.IsDBNull(indiceRol)
                    ? string.Empty
                    : reader.GetString(indiceRol);
            }

            // Las invitaciones antiguas sin contraseña no son activables.
            if (string.IsNullOrWhiteSpace(passwordHash) ||
                (rolUsuario != "Administrador" &&
                 rolUsuario != "Recepcionista"))
            {
                transaction.Rollback();
                errorMessage =
                    "La invitación no tiene credenciales o rol válidos.";
                return false;
            }

            // Crear usuario dentro de la misma transacción.
            using (DbCommand insertar = connection.CreateCommand())
            {
                insertar.Transaction = transaction;

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

                AddParameter(insertar, "@Username", username);
                AddParameter(insertar, "@PasswordHash", passwordHash);
                AddParameter(insertar, "@Email", email);
                AddParameter(insertar, "@Rol", rolUsuario);
                AddParameter(insertar, "@CreadoPor", "sistema");

                if (insertar.ExecuteNonQuery() != 1)
                {
                    transaction.Rollback();
                    errorMessage = "No se pudo crear el usuario.";
                    return false;
                }
            }

            // Confirmar el correo y aprobar la solicitud
            // dentro de la misma transacción.
            using (DbCommand actualizar = connection.CreateCommand())
            {
                actualizar.Transaction = transaction;

                actualizar.CommandText = """
                    UPDATE SolicitudesRegistro
                    SET Estado = 'Aprobada',
                        EmailVerificadoEn = UTC_TIMESTAMP(6),
                        FechaResolucion = UTC_TIMESTAMP(6),
                        TokenVerificacionHash = NULL,
                        TokenExpiraEn = NULL,
                        RevisadoPorUsuarioId = NULL
                    WHERE Id = @SolicitudId
                      AND TokenVerificacionHash = @TokenHash
                      AND Estado = 'PendienteVerificacion'
                      AND EmailVerificadoEn IS NULL
                      AND TokenExpiraEn > UTC_TIMESTAMP(6);
                    """;

                AddParameter(actualizar, "@SolicitudId", solicitudId);
                AddParameter(actualizar, "@TokenHash", tokenHash);

                if (actualizar.ExecuteNonQuery() != 1)
                {
                    transaction.Rollback();
                    errorMessage =
                        "No se pudo completar la activación.";
                    return false;
                }
            }

            // Las dos operaciones se guardan juntas.
            transaction.Commit();
        }
        catch
        {
            try
            {
                transaction.Rollback();
            }
            catch
            {
                // La transacción puede haberse cerrado
                // o la conexión puede haber fallado.
            }

            throw;
        }

        // Auditoría posterior a la confirmación de la transacción.
        _auditoria.Registrar(new RegistroAuditoria
        {
            UsuarioId = null,
            Username = username,
            Accion = "USUARIO_ACTIVADO",
            Entidad = "Usuario",
            EntidadId = username,
            Fecha = DateTime.UtcNow,
            Detalle = $"Rol asignado: {rolUsuario}"
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