
using System.Data.Common;
using TallerMecanico.Application.Ports;
using TallerMecanico.Data.Factories;
using TallerMecanico.Models;

namespace TallerMecanico.Infraestructura.Adapters.MySql;

public sealed class UsuarioRepository : IUsuarioPort
{
    private readonly DatabaseConnectionFactory _connectionFactory;

    public UsuarioRepository(
        DatabaseConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public Usuario? ObtenerPorUsername(string username)
    {
        using DbConnection connection =
            _connectionFactory.CreateConnection();

        connection.Open();

        const string query = """
            SELECT Id,
                   Username,
                   PasswordHash,
                   Email,
                   EmailVerificado,
                   Rol,
                   Activo,
                   CreadoPor,
                   FechaCreacion,
                   ModificadoPor,
                   FechaModificacion
            FROM Usuarios
            WHERE Username = @Username
            LIMIT 1;
            """;

        using DbCommand command =
            connection.CreateCommand();

        command.CommandText = query;

        AddParameter(command, "@Username", username);

        using DbDataReader reader =
            command.ExecuteReader();

        return reader.Read()
            ? MapUsuario(reader)
            : null;
    }

    public Usuario? ObtenerPorId(int id)
    {
        using DbConnection connection =
            _connectionFactory.CreateConnection();

        connection.Open();

        const string query = """
            SELECT Id,
                   Username,
                   PasswordHash,
                   Email,
                   EmailVerificado,
                   Rol,
                   Activo,
                   CreadoPor,
                   FechaCreacion,
                   ModificadoPor,
                   FechaModificacion
            FROM Usuarios
            WHERE Id = @Id;
            """;

        using DbCommand command =
            connection.CreateCommand();

        command.CommandText = query;

        AddParameter(command, "@Id", id);

        using DbDataReader reader =
            command.ExecuteReader();

        return reader.Read()
            ? MapUsuario(reader)
            : null;
    }

    public bool ExisteEmail(string email)
    {
        using DbConnection connection =
            _connectionFactory.CreateConnection();

        connection.Open();

        const string query = """
            SELECT COUNT(*)
            FROM Usuarios
            WHERE Email = @Email;
            """;

        using DbCommand command = connection.CreateCommand();
        command.CommandText = query;

        AddParameter(command, "@Email", email);

        return Convert.ToInt64(command.ExecuteScalar()) > 0;
    }
    public bool ExisteUsername(string username)
    {
        using DbConnection connection =
            _connectionFactory.CreateConnection();

        connection.Open();

        const string query = """
            SELECT COUNT(*)
            FROM Usuarios
            WHERE Username = @Username;
            """;

        using DbCommand command =
            connection.CreateCommand();

        command.CommandText = query;

        AddParameter(command, "@Username", username);

        return Convert.ToInt32(
            command.ExecuteScalar()) > 0;
    }

    public void Agregar(Usuario usuario)
    {
        using DbConnection connection =
            _connectionFactory.CreateConnection();

        connection.Open();

        const string query = """
            INSERT INTO Usuarios
            (
                Username,
                PasswordHash,
                Email,
                EmailVerificado,
                Rol,
                Activo,
                CreadoPor,
                FechaCreacion,
                ModificadoPor,
                FechaModificacion
            )
            VALUES
            (
                @Username,
                @PasswordHash,
                @Email,
                @EmailVerificado,
                @Rol,
                @Activo,
                @CreadoPor,
                @FechaCreacion,
                @ModificadoPor,
                @FechaModificacion
            );
            """;

        using DbCommand command =
            connection.CreateCommand();

        command.CommandText = query;

        AddUsuarioParameters(command, usuario);

        command.ExecuteNonQuery();
    }

    public void Actualizar(Usuario usuario)
    {
        using DbConnection connection =
            _connectionFactory.CreateConnection();

        connection.Open();

        const string query = """
            UPDATE Usuarios
            SET Username = @Username,
                PasswordHash = @PasswordHash,
                Email = @Email,
                EmailVerificado = @EmailVerificado,
                Rol = @Rol,
                Activo = @Activo,
                ModificadoPor = @ModificadoPor,
                FechaModificacion = @FechaModificacion
            WHERE Id = @Id;
            """;

        using DbCommand command =
            connection.CreateCommand();

        command.CommandText = query;

        AddParameter(command, "@Id", usuario.Id);
        AddParameter(command, "@Username", usuario.Username);
        AddParameter(command, "@PasswordHash", usuario.PasswordHash);
        AddParameter(command, "@Email", usuario.Email);
        AddParameter(command, "@EmailVerificado", usuario.EmailVerificado);
        AddParameter(command, "@Rol", usuario.Rol);
        AddParameter(command, "@Activo", usuario.Activo);
        AddParameter(command, "@ModificadoPor", usuario.ModificadoPor);
        AddParameter(
            command,
            "@FechaModificacion",
            usuario.FechaModificacion);

        command.ExecuteNonQuery();
    }

    private static void AddUsuarioParameters(
        DbCommand command,
        Usuario usuario)
    {
        AddParameter(command, "@Username", usuario.Username);
        AddParameter(command, "@PasswordHash", usuario.PasswordHash);
        AddParameter(command, "@Email", usuario.Email);
        AddParameter(command, "@EmailVerificado", usuario.EmailVerificado);
        AddParameter(command, "@Rol", usuario.Rol);
        AddParameter(command, "@Activo", usuario.Activo);
        AddParameter(command, "@CreadoPor", usuario.CreadoPor);
        AddParameter(command, "@FechaCreacion", usuario.FechaCreacion);
        AddParameter(command, "@ModificadoPor", usuario.ModificadoPor);
        AddParameter(
            command,
            "@FechaModificacion",
            usuario.FechaModificacion);
    }

    private static Usuario MapUsuario(DbDataReader reader)
    {
        return new Usuario
        {
            Id = reader.GetInt32(
                reader.GetOrdinal("Id")),

            Username = reader.GetString(
                reader.GetOrdinal("Username")),

            PasswordHash = reader.GetString(
                reader.GetOrdinal("PasswordHash")),

            Email = LeerStringOpcional(
                reader,
                "Email"),

            EmailVerificado = reader.GetBoolean(
                reader.GetOrdinal("EmailVerificado")),
            Rol = reader.GetString(
                reader.GetOrdinal("Rol")),

            Activo = reader.GetBoolean(
                reader.GetOrdinal("Activo")),

            CreadoPor = reader.GetString(
                reader.GetOrdinal("CreadoPor")),

            FechaCreacion = reader.GetDateTime(
                reader.GetOrdinal("FechaCreacion")),

            ModificadoPor = LeerStringOpcional(
                reader,
                "ModificadoPor"),

            FechaModificacion = LeerFechaOpcional(
                reader,
                "FechaModificacion")
        };
    }

    private static string? LeerStringOpcional(
        DbDataReader reader,
        string columna)
    {
        int indice = reader.GetOrdinal(columna);

        return reader.IsDBNull(indice)
            ? null
            : reader.GetString(indice);
    }

    private static DateTime? LeerFechaOpcional(
        DbDataReader reader,
        string columna)
    {
        int indice = reader.GetOrdinal(columna);

        return reader.IsDBNull(indice)
            ? null
            : reader.GetDateTime(indice);
    }

    private static void AddParameter(
        DbCommand command,
        string nombre,
        object? valor)
    {
        DbParameter parameter =
            command.CreateParameter();

        parameter.ParameterName = nombre;
        parameter.Value = valor ?? DBNull.Value;

        command.Parameters.Add(parameter);
    }
}
