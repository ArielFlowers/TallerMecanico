
using System.Data.Common;
using TallerMecanico.Aplicacion.DTOs;
using TallerMecanico.Aplicacion.Puertos;
using TallerMecanico.Infraestructura.Persistencia;
using TallerMecanico.Dominio.Entidades;

namespace TallerMecanico.Infraestructura.Persistencia.MySql;

public sealed class UsuarioRepository : IUsuarioAdministracionPort
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

    public IReadOnlyList<UsuarioListadoDto> Listar(int pagina, int tamanoPagina)
    {
        if (pagina < 1) pagina = 1;
        if (tamanoPagina < 1) tamanoPagina = 10;
        if (tamanoPagina > 100) tamanoPagina = 100;
        if (pagina > 10000) pagina = 10000;

        long offset = (long)(pagina - 1) * tamanoPagina;

        using DbConnection connection =
            _connectionFactory.CreateConnection();

        connection.Open();

        const string query = """
            SELECT Id,
                   Username,
                   Email,
                   Rol,
                   Activo,
                   FechaCreacion
            FROM Usuarios
            ORDER BY FechaCreacion DESC, Id DESC
            LIMIT @TamanoPagina OFFSET @Offset;
            """;

        using DbCommand command =
            connection.CreateCommand();

        command.CommandText = query;

        AddParameter(command, "@TamanoPagina", tamanoPagina);
        AddParameter(command, "@Offset", offset);

        using DbDataReader reader =
            command.ExecuteReader();

        var resultados = new List<UsuarioListadoDto>();

        while (reader.Read())
        {
            resultados.Add(MapUsuarioListado(reader));
        }

        return resultados;
    }

    public IReadOnlyList<UsuarioListadoDto> ListarFiltrados(
        string? busqueda, string? rol, bool? activo,
        int pagina, int tamanoPagina)
    {
        pagina = Math.Clamp(pagina, 1, 10000);
        tamanoPagina = Math.Clamp(tamanoPagina, 1, 100);
        long offset = (long)(pagina - 1) * tamanoPagina;

        using DbConnection connection = _connectionFactory.CreateConnection();
        connection.Open();

        using DbCommand command = connection.CreateCommand();
        command.CommandText = """
            SELECT Id, Username, Email, Rol, Activo, FechaCreacion
            FROM Usuarios
            WHERE (@Busqueda IS NULL OR Username LIKE CONCAT('%', @Busqueda, '%')
                   OR Email LIKE CONCAT('%', @Busqueda, '%'))
              AND (@Rol IS NULL OR Rol = @Rol)
              AND (@Activo IS NULL OR Activo = @Activo)
            ORDER BY FechaCreacion DESC, Id DESC
            LIMIT @TamanoPagina OFFSET @Offset;
            """;

        AddParameter(command, "@Busqueda", string.IsNullOrWhiteSpace(busqueda) ? null : busqueda.Trim());
        AddParameter(command, "@Rol", string.IsNullOrWhiteSpace(rol) ? null : rol.Trim());
        AddParameter(command, "@Activo", activo.HasValue ? (object)activo.Value : DBNull.Value);
        AddParameter(command, "@TamanoPagina", tamanoPagina);
        AddParameter(command, "@Offset", offset);

        using DbDataReader reader = command.ExecuteReader();
        var resultados = new List<UsuarioListadoDto>();

        while (reader.Read())
        {
            resultados.Add(MapUsuarioListado(reader));
        }

        return resultados;
    }

    public int ContarFiltrados(string? busqueda, string? rol, bool? activo)
    {
        using DbConnection connection = _connectionFactory.CreateConnection();
        connection.Open();

        using DbCommand command = connection.CreateCommand();
        command.CommandText = """
            SELECT COUNT(*)
            FROM Usuarios
            WHERE (@Busqueda IS NULL OR Username LIKE CONCAT('%', @Busqueda, '%')
                   OR Email LIKE CONCAT('%', @Busqueda, '%'))
              AND (@Rol IS NULL OR Rol = @Rol)
              AND (@Activo IS NULL OR Activo = @Activo);
            """;

        AddParameter(command, "@Busqueda", string.IsNullOrWhiteSpace(busqueda) ? null : busqueda.Trim());
        AddParameter(command, "@Rol", string.IsNullOrWhiteSpace(rol) ? null : rol.Trim());
        AddParameter(command, "@Activo", activo.HasValue ? (object)activo.Value : DBNull.Value);

        return Convert.ToInt32(command.ExecuteScalar() ?? 0);
    }

    public int ContarUsuariosActivos()
    {
        using DbConnection connection = _connectionFactory.CreateConnection();
        connection.Open();

        using DbCommand command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM Usuarios WHERE Activo = 1;";

        return Convert.ToInt32(command.ExecuteScalar() ?? 0);
    }

    public int ContarAdministradoresActivosListado()
    {
        return ContarAdministradoresActivos();
    }
    public int ContarTotal()
    {
        using DbConnection connection =
            _connectionFactory.CreateConnection();

        connection.Open();

        const string query = """
            SELECT COUNT(*)
            FROM Usuarios;
            """;

        using DbCommand command =
            connection.CreateCommand();

        command.CommandText = query;

        object? resultado = command.ExecuteScalar();

        return Convert.ToInt32(resultado ?? 0);
    }

    public int ContarAdministradoresActivos()
    {
        using DbConnection connection =
            _connectionFactory.CreateConnection();

        connection.Open();

        const string query = """
            SELECT COUNT(*)
            FROM Usuarios
            WHERE Rol = 'Administrador' AND Activo = 1;
            """;

        using DbCommand command =
            connection.CreateCommand();

        command.CommandText = query;

        object? resultado = command.ExecuteScalar();

        return Convert.ToInt32(resultado ?? 0);
    }

    public bool ActualizarUsuarioAtomico(int id, string email, string rol, bool activo, string modificadoPor, out string errorMessage)
    {
        errorMessage = string.Empty;

        using DbConnection connection = _connectionFactory.CreateConnection();
        connection.Open();

        using DbTransaction transaction = connection.BeginTransaction();

        try
        {
            using DbCommand lockCommand = connection.CreateCommand();
            lockCommand.Transaction = transaction;

            lockCommand.CommandText = """
                SELECT Id FROM Usuarios
                WHERE Rol = 'Administrador' AND Activo = 1
                FOR UPDATE;
                """;

            using DbDataReader reader = lockCommand.ExecuteReader();
            var adminIds = new List<int>();
            while (reader.Read())
            {
                adminIds.Add(reader.GetInt32(0));
            }
            reader.Close();

            int adminsActivos = adminIds.Count;

            bool esAdminActual = adminIds.Contains(id);
            bool seraAdminActivo = activo && rol == "Administrador";

            if (esAdminActual && !seraAdminActivo && adminsActivos <= 1)
            {
                transaction.Rollback();
                errorMessage = "No puede desactivar el último administrador activo.";
                return false;
            }

            using DbCommand updateCommand = connection.CreateCommand();
            updateCommand.Transaction = transaction;

            updateCommand.CommandText = """
                UPDATE Usuarios
                SET Email = @Email,
                    Rol = @Rol,
                    Activo = @Activo,
                    ModificadoPor = @ModificadoPor,
                    FechaModificacion = UTC_TIMESTAMP(6)
                WHERE Id = @Id;
                """;

            AddParameter(updateCommand, "@Id", id);
            AddParameter(updateCommand, "@Email", email);
            AddParameter(updateCommand, "@Rol", rol);
            AddParameter(updateCommand, "@Activo", activo);
            AddParameter(updateCommand, "@ModificadoPor", modificadoPor);

            int filas = updateCommand.ExecuteNonQuery();

            if (filas != 1)
            {
                transaction.Rollback();
                errorMessage = "Usuario no encontrado.";
                return false;
            }

            transaction.Commit();
            return true;
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public bool DesactivarUsuarioAtomico(int id, string modificadoPor, out string errorMessage)
    {
        errorMessage = string.Empty;

        using DbConnection connection = _connectionFactory.CreateConnection();
        connection.Open();

        using DbTransaction transaction = connection.BeginTransaction();

        try
        {
            using DbCommand lockCommand = connection.CreateCommand();
            lockCommand.Transaction = transaction;

            lockCommand.CommandText = """
                SELECT Id FROM Usuarios
                WHERE Rol = 'Administrador' AND Activo = 1
                FOR UPDATE;
                """;

            using DbDataReader reader = lockCommand.ExecuteReader();
            var adminIds = new List<int>();
            while (reader.Read())
            {
                adminIds.Add(reader.GetInt32(0));
            }
            reader.Close();

            int adminsActivos = adminIds.Count;

            if (adminIds.Contains(id) && adminsActivos <= 1)
            {
                transaction.Rollback();
                errorMessage = "No puede desactivar el último administrador activo.";
                return false;
            }

            using DbCommand updateCommand = connection.CreateCommand();
            updateCommand.Transaction = transaction;

            updateCommand.CommandText = """
                UPDATE Usuarios
                SET Activo = 0,
                    ModificadoPor = @ModificadoPor,
                    FechaModificacion = UTC_TIMESTAMP(6)
                WHERE Id = @Id AND Activo = 1;
                """;

            AddParameter(updateCommand, "@Id", id);
            AddParameter(updateCommand, "@ModificadoPor", modificadoPor);

            int filas = updateCommand.ExecuteNonQuery();

            if (filas != 1)
            {
                transaction.Rollback();
                errorMessage = "El usuario ya está desactivado o no existe.";
                return false;
            }

            transaction.Commit();
            return true;
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public Usuario? ObtenerPorIdConCredenciales(int id)
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

    private static UsuarioListadoDto MapUsuarioListado(DbDataReader reader)
    {
        return new UsuarioListadoDto(
            reader.GetInt32(reader.GetOrdinal("Id")),
            reader.GetString(reader.GetOrdinal("Username")),
            LeerStringOpcional(reader, "Email") ?? string.Empty,
            reader.GetString(reader.GetOrdinal("Rol")),
            reader.GetBoolean(reader.GetOrdinal("Activo")),
            reader.GetDateTime(reader.GetOrdinal("FechaCreacion")));
    }
}
