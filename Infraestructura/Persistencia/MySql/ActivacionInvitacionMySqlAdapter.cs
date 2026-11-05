
using System.Data.Common;
using TallerMecanico.Aplicacion.DTOs;
using TallerMecanico.Aplicacion.Puertos;
using TallerMecanico.Infraestructura.Persistencia;

namespace TallerMecanico.Infraestructura.Persistencia.MySql;

public sealed class ActivacionInvitacionMySqlAdapter
    : IActivacionInvitacionPort
{
    private readonly DatabaseConnectionFactory _connectionFactory;

    public ActivacionInvitacionMySqlAdapter(
        DatabaseConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public ResultadoActivacionInvitacion ActivarPorTokenHash(
        string tokenHash)
    {
        const string errorInvitacion =
            "El enlace de invitación no es válido o ha expirado.";

        if (string.IsNullOrWhiteSpace(tokenHash) ||
            tokenHash.Length != 64 ||
            !tokenHash.All(Uri.IsHexDigit))
        {
            return new ResultadoActivacionInvitacion(
                false, null, null, errorInvitacion);
        }

        using DbConnection connection =
            _connectionFactory.CreateConnection();

        connection.Open();

        using DbTransaction transaction =
            connection.BeginTransaction();

        string username;
        string email;
        string passwordHash;
        string rolUsuario;
        string creadoPor;
        long solicitudId;

        try
        {
            // Buscar y bloquear exclusivamente la solicitud
            // correspondiente al token recibido.
            using (DbCommand buscar = connection.CreateCommand())
            {
                buscar.Transaction = transaction;

                buscar.CommandText = """
                    SELECT Id, Username, Email, PasswordHash, Rol, CreadoPor
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
                    reader.Close();

                    transaction.Rollback();

                    return new ResultadoActivacionInvitacion(
                        false, null, null, errorInvitacion);
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

                int indiceCreadoPor = reader.GetOrdinal("CreadoPor");

                creadoPor = reader.IsDBNull(indiceCreadoPor)
                    ? "sistema"
                    : reader.GetString(indiceCreadoPor);
            }

            // Comprobar que las credenciales y el rol sean válidos.
            if (string.IsNullOrWhiteSpace(passwordHash) ||
                (rolUsuario != "Administrador" &&
                 rolUsuario != "Recepcionista"))
            {
                transaction.Rollback();

                return new ResultadoActivacionInvitacion(
                    false,
                    null,
                    null,
                    "La invitación no tiene credenciales o rol válidos.");
            }

            // Crear el usuario dentro de la misma transacción.
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
                AddParameter(insertar, "@CreadoPor", creadoPor);

                if (insertar.ExecuteNonQuery() != 1)
                {
                    transaction.Rollback();

                    return new ResultadoActivacionInvitacion(
                        false,
                        null,
                        null,
                        "No se pudo crear el usuario.");
                }
            }

            // Aprobar la solicitud y confirmar el correo
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

                    return new ResultadoActivacionInvitacion(
                        false,
                        null,
                        null,
                        "No se pudo completar la activación.");
                }
            }

            // Confirmar ambas operaciones juntas.
            transaction.Commit();

            return new ResultadoActivacionInvitacion(
                true, username, rolUsuario, null);
        }
        catch
        {
            try
            {
                transaction.Rollback();
            }
            catch
            {
                // La transacción podría haberse cerrado
                // o la conexión podría haber fallado.
            }

            throw;
        }
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
