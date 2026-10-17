using System.Data.Common;
using TallerMecanico.Aplicacion.Dto;
using TallerMecanico.Aplicacion.Puertos;
using TallerMecanico.Infraestructura.Persistencia;
using TallerMecanico.Dominio.Entidades;

namespace TallerMecanico.Infraestructura.Persistencia.MySql;

public class ClienteRepository :
    IRepository<Cliente>,
    IClientePort
{
    private const int MaximoSugerencias = 10;
    private readonly DatabaseConnectionFactory _connectionFactory;

    public ClienteRepository(
        DatabaseConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public List<Cliente> GetAll()
    {
        List<Cliente> clientes = [];

        using DbConnection connection =
            _connectionFactory.CreateConnection();

        connection.Open();

        const string query = """
            SELECT Id,
                   Ci,
                   ComplementoCi,
                   Nombres,
                   PrimerApellido,
                   SegundoApellido,
                   Celular,
                   CreadoPor,
                   FechaCreacion,
                   ModificadoPor,
                   FechaModificacion
            FROM Clientes
            ORDER BY PrimerApellido ASC,
                     SegundoApellido ASC,
                     Nombres ASC,
                     Ci ASC,
                     ComplementoCi ASC;
            """;

        using DbCommand command =
            connection.CreateCommand();

        command.CommandText = query;

        using DbDataReader reader =
            command.ExecuteReader();

        while (reader.Read())
        {
            clientes.Add(
                MapCliente(reader));
        }

        return clientes;
    }

    IReadOnlyList<Cliente> IClientePort.GetAll()
    {
        return GetAll();
    }

    public Cliente? GetById(int id)
    {
        using DbConnection connection =
            _connectionFactory.CreateConnection();

        connection.Open();

        const string query = """
            SELECT Id,
                   Ci,
                   ComplementoCi,
                   Nombres,
                   PrimerApellido,
                   SegundoApellido,
                   Celular,
                   CreadoPor,
                   FechaCreacion,
                   ModificadoPor,
                   FechaModificacion
            FROM Clientes
            WHERE Id = @Id;
            """;

        using DbCommand command =
            connection.CreateCommand();

        command.CommandText = query;

        AddParameter(
            command,
            "@Id",
            id);

        using DbDataReader reader =
            command.ExecuteReader();

        if (!reader.Read())
        {
            return null;
        }

        return MapCliente(reader);
    }

    public IReadOnlyList<Cliente> Search(
        string terminoBusqueda)
    {
        List<Cliente> clientes = [];

        using DbConnection connection =
            _connectionFactory.CreateConnection();

        connection.Open();

        const string query = """
            SELECT Id,
                   Ci,
                   ComplementoCi,
                   Nombres,
                   PrimerApellido,
                   SegundoApellido,
                   Celular,
                   CreadoPor,
                   FechaCreacion,
                   ModificadoPor,
                   FechaModificacion
            FROM Clientes
            WHERE Ci LIKE @PatronBusqueda ESCAPE '!'
               OR ComplementoCi LIKE @PatronBusqueda ESCAPE '!'
               OR CONCAT(Ci, '-', ComplementoCi)
                    LIKE @PatronBusqueda ESCAPE '!'
               OR Nombres LIKE @PatronBusqueda ESCAPE '!'
               OR PrimerApellido LIKE @PatronBusqueda ESCAPE '!'
               OR SegundoApellido LIKE @PatronBusqueda ESCAPE '!'
               OR CONCAT(
                    Nombres,
                    ' ',
                    PrimerApellido,
                    ' ',
                    SegundoApellido
                  ) LIKE @PatronBusqueda ESCAPE '!'
               OR Celular LIKE @PatronBusqueda ESCAPE '!'
            ORDER BY PrimerApellido ASC,
                     SegundoApellido ASC,
                     Nombres ASC,
                     Ci ASC,
                     ComplementoCi ASC;
            """;

        using DbCommand command =
            connection.CreateCommand();

        command.CommandText = query;

        AddParameter(
            command,
            "@PatronBusqueda",
            $"%{EscaparPatron(terminoBusqueda)}%");

        using DbDataReader reader =
            command.ExecuteReader();

        while (reader.Read())
        {
            clientes.Add(
                MapCliente(reader));
        }

        return clientes;
    }

    public async Task<IReadOnlyList<SugerenciaCliente>> SugerirAsync(
        string termino, CancellationToken cancellationToken = default)
    {
        var palabras = termino.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (palabras.Length == 0)
        {
            return [];
        }

        await using DbConnection connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using DbCommand command = connection.CreateCommand();
        var condicionesPalabras = new List<string>();

        for (int indice = 0; indice < palabras.Length; indice++)
        {
            condicionesPalabras.Add($"""
                (Nombres COLLATE utf8mb4_0900_ai_ci LIKE @Inicio{indice} ESCAPE '!'
                 OR Nombres COLLATE utf8mb4_0900_ai_ci LIKE @Palabra{indice} ESCAPE '!'
                 OR PrimerApellido COLLATE utf8mb4_0900_ai_ci LIKE @Inicio{indice} ESCAPE '!'
                 OR PrimerApellido COLLATE utf8mb4_0900_ai_ci LIKE @Palabra{indice} ESCAPE '!'
                 OR SegundoApellido COLLATE utf8mb4_0900_ai_ci LIKE @Inicio{indice} ESCAPE '!'
                 OR SegundoApellido COLLATE utf8mb4_0900_ai_ci LIKE @Palabra{indice} ESCAPE '!')
                """);
            var palabraEscapada = EscaparPatron(palabras[indice]);
            AddParameter(command, $"@Inicio{indice}", $"{palabraEscapada}%");
            AddParameter(command, $"@Palabra{indice}", $"% {palabraEscapada}%");
        }

        command.CommandText = $"""
            SELECT Id, Ci, ComplementoCi, Nombres, PrimerApellido, SegundoApellido
            FROM Clientes
            WHERE Ci LIKE @PrefijoCi ESCAPE '!'
               OR CONCAT(Ci, '-', ComplementoCi) COLLATE utf8mb4_0900_ai_ci
                    LIKE @PrefijoCi ESCAPE '!'
               OR ({string.Join(" AND ", condicionesPalabras)})
            ORDER BY PrimerApellido ASC, SegundoApellido ASC, Nombres ASC,
                     Ci ASC, ComplementoCi ASC, Id ASC
            LIMIT @Limite;
            """;
        AddParameter(command, "@PrefijoCi", $"{EscaparPatron(termino)}%");
        AddParameter(command, "@Limite", MaximoSugerencias);

        var sugerencias = new List<SugerenciaCliente>();
        await using DbDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            sugerencias.Add(MapSugerencia(reader));
        }

        return sugerencias;
    }

    private static SugerenciaCliente MapSugerencia(DbDataReader reader)
    {
        var nombres = new[]
        {
            reader.GetString(3), reader.GetString(4),
            reader.IsDBNull(5) ? string.Empty : reader.GetString(5)
        };
        var ci = reader.GetString(1);
        var complemento = reader.GetString(2);
        var ciCompleto = string.IsNullOrEmpty(complemento) ? ci : $"{ci}-{complemento}";
        return new SugerenciaCliente(
            reader.GetInt32(0),
            string.Join(" ", nombres.Where(nombre => !string.IsNullOrWhiteSpace(nombre)).Select(nombre => nombre.Trim())),
            $"CI: {ciCompleto}");
    }

    public bool ExistsByCi(
        string ci,
        string complementoCi,
        int idExcluido = 0)
    {
        using DbConnection connection =
            _connectionFactory.CreateConnection();

        connection.Open();

        const string query = """
            SELECT COUNT(*)
            FROM Clientes
            WHERE Ci = @Ci
              AND ComplementoCi = @ComplementoCi
              AND Id <> @IdExcluido;
            """;

        using DbCommand command =
            connection.CreateCommand();

        command.CommandText = query;

        AddParameter(command, "@Ci", ci);
        AddParameter(command, "@ComplementoCi", complementoCi);
        AddParameter(command, "@IdExcluido", idExcluido);

        return Convert.ToInt32(
            command.ExecuteScalar()) > 0;
    }

    public void Add(Cliente cliente)
    {
        using DbConnection connection =
            _connectionFactory.CreateConnection();

        connection.Open();

        const string query = """
            INSERT INTO Clientes
            (
                Ci,
                ComplementoCi,
                Nombres,
                PrimerApellido,
                SegundoApellido,
                Celular,
                CreadoPor,
                FechaCreacion
            )
            VALUES
            (
                @Ci,
                @ComplementoCi,
                @Nombres,
                @PrimerApellido,
                @SegundoApellido,
                @Celular,
                @CreadoPor,
                @FechaCreacion
            );
            """;

        using DbCommand command =
            connection.CreateCommand();

        command.CommandText = query;

        AddParameters(
            command,
            cliente);

        command.ExecuteNonQuery();

        command.CommandText = "SELECT LAST_INSERT_ID();";
        cliente.Id = Convert.ToInt32(command.ExecuteScalar());
    }

    public void Update(Cliente cliente)
    {
        using DbConnection connection =
            _connectionFactory.CreateConnection();

        connection.Open();

        const string query = """
            UPDATE Clientes
            SET Ci = @Ci,
                ComplementoCi = @ComplementoCi,
                Nombres = @Nombres,
                PrimerApellido = @PrimerApellido,
                SegundoApellido = @SegundoApellido,
                Celular = @Celular,
                ModificadoPor = @ModificadoPor,
                FechaModificacion = @FechaModificacion
            WHERE Id = @Id;
            """;

        using DbCommand command =
            connection.CreateCommand();

        command.CommandText = query;

        AddParameter(command, "@Ci", cliente.Ci);
        AddParameter(command, "@ComplementoCi", cliente.ComplementoCi);
        AddParameter(command, "@Nombres", cliente.Nombres);
        AddParameter(command, "@PrimerApellido", cliente.PrimerApellido);
        AddParameter(command, "@SegundoApellido", cliente.SegundoApellido);
        AddParameter(command, "@Celular", cliente.Celular);
        AddParameter(command, "@ModificadoPor", cliente.ModificadoPor);
        AddParameter(command, "@FechaModificacion", cliente.FechaModificacion);
        AddParameter(command, "@Id", cliente.Id);

        command.ExecuteNonQuery();
    }

    public void Delete(int id)
    {
        using DbConnection connection =
            _connectionFactory.CreateConnection();

        connection.Open();

        const string query = """
            DELETE FROM Clientes
            WHERE Id = @Id;
            """;

        using DbCommand command =
            connection.CreateCommand();

        command.CommandText = query;

        AddParameter(command, "@Id", id);

        command.ExecuteNonQuery();
    }

    public int Count()
    {
        using DbConnection connection =
            _connectionFactory.CreateConnection();

        connection.Open();

        const string query = """
            SELECT COUNT(*)
            FROM Clientes;
            """;

        using DbCommand command =
            connection.CreateCommand();

        command.CommandText = query;

        return Convert.ToInt32(
            command.ExecuteScalar());
    }

    private static void AddParameters(
        DbCommand command,
        Cliente cliente)
    {
        AddParameter(command, "@Ci", cliente.Ci);
        AddParameter(command, "@ComplementoCi", cliente.ComplementoCi);
        AddParameter(command, "@Nombres", cliente.Nombres);
        AddParameter(command, "@PrimerApellido", cliente.PrimerApellido);
        AddParameter(command, "@SegundoApellido", cliente.SegundoApellido);
        AddParameter(command, "@Celular", cliente.Celular);
        AddParameter(command, "@CreadoPor", cliente.CreadoPor);
        AddParameter(command, "@FechaCreacion", cliente.FechaCreacion);
    }

    private static Cliente MapCliente(
        DbDataReader reader)
    {
        return new Cliente
        {
            Id =
                reader.GetInt32(
                    reader.GetOrdinal("Id")),

            Ci =
                reader.GetString(
                    reader.GetOrdinal("Ci")),

            ComplementoCi =
                reader.GetString(
                    reader.GetOrdinal("ComplementoCi")),

            Nombres =
                reader.GetString(
                    reader.GetOrdinal("Nombres")),

            PrimerApellido =
                reader.GetString(
                    reader.GetOrdinal("PrimerApellido")),

            SegundoApellido =
                reader.GetString(
                    reader.GetOrdinal("SegundoApellido")),

            Celular =
                reader.GetString(
                    reader.GetOrdinal("Celular")),

            CreadoPor =
                reader.GetString(
                    reader.GetOrdinal("CreadoPor")),

            FechaCreacion =
                reader.GetDateTime(
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

    private static string EscaparPatron(
        string terminoBusqueda)
    {
        return terminoBusqueda
            .Trim()
            .Replace(
                "!",
                "!!",
                StringComparison.Ordinal)
            .Replace(
                "%",
                "!%",
                StringComparison.Ordinal)
            .Replace(
                "_",
                "!_",
                StringComparison.Ordinal);
    }
}
