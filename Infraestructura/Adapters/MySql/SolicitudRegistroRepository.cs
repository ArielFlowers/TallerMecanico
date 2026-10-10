using System.Data.Common;
using TallerMecanico.Application.Ports;
using TallerMecanico.Data.Factories;
using TallerMecanico.Models;

namespace TallerMecanico.Infraestructura.Adapters.MySql;

public sealed class SolicitudRegistroRepository : ISolicitudRegistroPort
{
    private readonly DatabaseConnectionFactory _connectionFactory;

    public SolicitudRegistroRepository(
        DatabaseConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public bool ExisteUsername(string username)
    {
        const string sql = """
            SELECT COUNT(*)
            FROM SolicitudesRegistro
            WHERE Username = @Username;
            """;

        return Existe(sql, "@Username", username);
    }

    public bool ExisteEmail(string email)
    {
        const string sql = """
            SELECT COUNT(*)
            FROM SolicitudesRegistro
            WHERE Email = @Email;
            """;

        return Existe(sql, "@Email", email);
    }

    public void Agregar(SolicitudRegistro solicitud)
    {
        using DbConnection connection =
            _connectionFactory.CreateConnection();

        connection.Open();

        const string sql = """
            INSERT INTO SolicitudesRegistro
            (
                Username,
                Email,
                PasswordHash,
                Estado,
                TokenVerificacionHash,
                TokenExpiraEn,
                EmailVerificadoEn,
                FechaSolicitud,
                UltimoIntentoVerificacionEn,
                FechaResolucion,
                RevisadoPorUsuarioId
            )
            VALUES
            (
                @Username,
                @Email,
                @PasswordHash,
                @Estado,
                @TokenVerificacionHash,
                @TokenExpiraEn,
                @EmailVerificadoEn,
                @FechaSolicitud,
                @UltimoIntentoVerificacionEn,
                @FechaResolucion,
                @RevisadoPorUsuarioId
            );
            """;

        using DbCommand command = connection.CreateCommand();
        command.CommandText = sql;

        AgregarParametros(command, solicitud);
        command.ExecuteNonQuery();
    }

    public SolicitudRegistro? ObtenerPorId(long id)
    {
        const string sql = """
            SELECT *
            FROM SolicitudesRegistro
            WHERE Id = @Id
            LIMIT 1;
            """;

        return Obtener(sql, "@Id", id);
    }

    public SolicitudRegistro? ObtenerPorTokenHash(string tokenHash)
    {
        const string sql = """
            SELECT *
            FROM SolicitudesRegistro
            WHERE TokenVerificacionHash = @TokenHash
            LIMIT 1;
            """;

        return Obtener(sql, "@TokenHash", tokenHash);
    }

    public SolicitudRegistro? ObtenerPendientePorEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return null;
        }

        const string sql = """
            SELECT *
            FROM SolicitudesRegistro
            WHERE Email = @Email
              AND Estado = 'PendienteVerificacion'
              AND EmailVerificadoEn IS NULL
            LIMIT 1;
            """;

        return Obtener(
            sql,
            "@Email",
            email.Trim().ToLowerInvariant());
    }
    public bool IntentarRenovarTokenConLimite(
        long solicitudId,
        string nuevoTokenHash,
        DateTime nuevaExpiracion)
    {
        if (solicitudId <= 0 ||
            string.IsNullOrWhiteSpace(nuevoTokenHash) ||
            nuevoTokenHash.Length != 64 ||
            !nuevoTokenHash.All(Uri.IsHexDigit) ||
            nuevaExpiracion <= DateTime.UtcNow)
        {
            return false;
        }

        using DbConnection connection =
            _connectionFactory.CreateConnection();

        connection.Open();

        const string sql = """
            UPDATE SolicitudesRegistro
            SET TokenVerificacionHash = @NuevoTokenHash,
                TokenExpiraEn = @NuevaExpiracion,
                UltimoIntentoVerificacionEn = UTC_TIMESTAMP(6)
            WHERE Id = @Id
              AND Estado = 'PendienteVerificacion'
              AND EmailVerificadoEn IS NULL
              AND (
                  UltimoIntentoVerificacionEn IS NULL
                  OR UltimoIntentoVerificacionEn <=
                     UTC_TIMESTAMP(6) - INTERVAL 2 MINUTE
              );
            """;

        using DbCommand command = connection.CreateCommand();
        command.CommandText = sql;

        AgregarParametro(command, "@Id", solicitudId);
        AgregarParametro(command, "@NuevoTokenHash", nuevoTokenHash);
        AgregarParametro(command, "@NuevaExpiracion", nuevaExpiracion);

        return command.ExecuteNonQuery() == 1;
    }
    public bool ConfirmarEmail(string tokenHash)
    {
        if (string.IsNullOrWhiteSpace(tokenHash) ||
            tokenHash.Length != 64)
        {
            return false;
        }

        using DbConnection connection =
            _connectionFactory.CreateConnection();

        connection.Open();

        const string sql = """
            UPDATE SolicitudesRegistro
            SET Estado = 'PendienteAprobacion',
                EmailVerificadoEn = UTC_TIMESTAMP(6),
                TokenVerificacionHash = NULL,
                TokenExpiraEn = NULL
            WHERE TokenVerificacionHash = @TokenHash
              AND Estado = 'PendienteVerificacion'
              AND TokenExpiraEn > UTC_TIMESTAMP(6)
              AND EmailVerificadoEn IS NULL;
            """;

        using DbCommand command = connection.CreateCommand();
        command.CommandText = sql;

        AgregarParametro(command, "@TokenHash", tokenHash);

        return command.ExecuteNonQuery() == 1;
    }
    public void Actualizar(SolicitudRegistro solicitud)
    {
        using DbConnection connection =
            _connectionFactory.CreateConnection();

        connection.Open();

        const string sql = """
            UPDATE SolicitudesRegistro
            SET Username = @Username,
                Email = @Email,
                PasswordHash = @PasswordHash,
                Estado = @Estado,
                TokenVerificacionHash = @TokenVerificacionHash,
                TokenExpiraEn = @TokenExpiraEn,
                EmailVerificadoEn = @EmailVerificadoEn,
                FechaSolicitud = @FechaSolicitud,
                UltimoIntentoVerificacionEn = @UltimoIntentoVerificacionEn,
                FechaResolucion = @FechaResolucion,
                RevisadoPorUsuarioId = @RevisadoPorUsuarioId
            WHERE Id = @Id;
            """;

        using DbCommand command = connection.CreateCommand();
        command.CommandText = sql;

        AgregarParametro(command, "@Id", solicitud.Id);
        AgregarParametros(command, solicitud);

        if (command.ExecuteNonQuery() != 1)
        {
            throw new InvalidOperationException(
                "No se pudo actualizar la solicitud.");
        }
    }

    private bool Existe(
        string sql,
        string parametro,
        object valor)
    {
        using DbConnection connection =
            _connectionFactory.CreateConnection();

        connection.Open();

        using DbCommand command = connection.CreateCommand();
        command.CommandText = sql;

        AgregarParametro(command, parametro, valor);

        return Convert.ToInt64(command.ExecuteScalar()) > 0;
    }

    private SolicitudRegistro? Obtener(
        string sql,
        string parametro,
        object valor)
    {
        using DbConnection connection =
            _connectionFactory.CreateConnection();

        connection.Open();

        using DbCommand command = connection.CreateCommand();
        command.CommandText = sql;

        AgregarParametro(command, parametro, valor);

        using DbDataReader reader = command.ExecuteReader();

        return reader.Read() ? Mapear(reader) : null;
    }

    private static SolicitudRegistro Mapear(DbDataReader reader)
    {
        return new SolicitudRegistro
        {
            Id = reader.GetInt64(reader.GetOrdinal("Id")),
            Username = reader.GetString(reader.GetOrdinal("Username")),
            Email = reader.GetString(reader.GetOrdinal("Email")),
            PasswordHash = reader.GetString(reader.GetOrdinal("PasswordHash")),
            Estado = reader.GetString(reader.GetOrdinal("Estado")),
            TokenVerificacionHash = LeerString(reader, "TokenVerificacionHash"),
            TokenExpiraEn = LeerFecha(reader, "TokenExpiraEn"),
            EmailVerificadoEn = LeerFecha(reader, "EmailVerificadoEn"),
            FechaSolicitud = reader.GetDateTime(reader.GetOrdinal("FechaSolicitud")),
            UltimoIntentoVerificacionEn = LeerFecha(reader, "UltimoIntentoVerificacionEn"),
            FechaResolucion = LeerFecha(reader, "FechaResolucion"),
            RevisadoPorUsuarioId = LeerEntero(reader, "RevisadoPorUsuarioId")
        };
    }

    private static string? LeerString(DbDataReader reader, string columna)
    {
        int indice = reader.GetOrdinal(columna);
        return reader.IsDBNull(indice) ? null : reader.GetString(indice);
    }

    private static DateTime? LeerFecha(DbDataReader reader, string columna)
    {
        int indice = reader.GetOrdinal(columna);
        return reader.IsDBNull(indice) ? null : reader.GetDateTime(indice);
    }

    private static int? LeerEntero(DbDataReader reader, string columna)
    {
        int indice = reader.GetOrdinal(columna);
        return reader.IsDBNull(indice) ? null : reader.GetInt32(indice);
    }

    private static void AgregarParametros(
        DbCommand command,
        SolicitudRegistro solicitud)
    {
        AgregarParametro(command, "@Username", solicitud.Username);
        AgregarParametro(command, "@Email", solicitud.Email);
        AgregarParametro(command, "@PasswordHash", solicitud.PasswordHash);
        AgregarParametro(command, "@Estado", solicitud.Estado);
        AgregarParametro(command, "@TokenVerificacionHash", solicitud.TokenVerificacionHash);
        AgregarParametro(command, "@TokenExpiraEn", solicitud.TokenExpiraEn);
        AgregarParametro(command, "@EmailVerificadoEn", solicitud.EmailVerificadoEn);
        AgregarParametro(command, "@FechaSolicitud", solicitud.FechaSolicitud);
        AgregarParametro(command, "@UltimoIntentoVerificacionEn", solicitud.UltimoIntentoVerificacionEn);
        AgregarParametro(command, "@FechaResolucion", solicitud.FechaResolucion);
        AgregarParametro(command, "@RevisadoPorUsuarioId", solicitud.RevisadoPorUsuarioId);
    }

    private static void AgregarParametro(
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