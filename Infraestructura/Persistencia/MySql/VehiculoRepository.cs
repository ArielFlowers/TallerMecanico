using System.Data.Common;
using TallerMecanico.Aplicacion.Dto;
using TallerMecanico.Aplicacion.Puertos;
using TallerMecanico.Infraestructura.Persistencia;
using TallerMecanico.Dominio.Entidades;

namespace TallerMecanico.Infraestructura.Persistencia.MySql;

public class VehiculoRepository :
    IRepository<Vehiculo>,
    IVehiculoPort
{
    private const int MaximoSugerencias = 10;
    private readonly DatabaseConnectionFactory _connectionFactory;

    public VehiculoRepository(
        DatabaseConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public List<Vehiculo> GetAll()
    {
        List<Vehiculo> vehiculos = [];

        using DbConnection connection =
            _connectionFactory.CreateConnection();

        connection.Open();

        const string query = """
            SELECT Id,
                   Placa,
                   Modelo,
                   Kilometraje,
                   Observaciones,
                   Marca,
                   ClienteId,
                   EsPlacaExtranjera,
                   CreadoPor,
                   FechaCreacion,
                   ModificadoPor,
                   FechaModificacion
            FROM Vehiculos
            ORDER BY Placa;
            """;

        using DbCommand command =
            connection.CreateCommand();

        command.CommandText = query;

        using DbDataReader reader =
            command.ExecuteReader();

        while (reader.Read())
        {
            vehiculos.Add(
                MapVehiculo(reader));
        }

        return vehiculos;
    }

    IReadOnlyList<Vehiculo> IVehiculoPort.GetAll()
    {
        return GetAll();
    }

    public Vehiculo? GetById(
        int id)
    {
        using DbConnection connection =
            _connectionFactory.CreateConnection();

        connection.Open();

        const string query = """
            SELECT Id,
                   Placa,
                   Modelo,
                   Kilometraje,
                   Observaciones,
                   Marca,
                   ClienteId,
                   EsPlacaExtranjera,
                   CreadoPor,
                   FechaCreacion,
                   ModificadoPor,
                   FechaModificacion
            FROM Vehiculos
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

        return MapVehiculo(reader);
    }

    public IReadOnlyList<Vehiculo> Search(
        string filtro)
    {
        List<Vehiculo> vehiculos = [];

        using DbConnection connection =
            _connectionFactory.CreateConnection();

        connection.Open();

        const string query = """
            SELECT Id,
                   Placa,
                   Modelo,
                   Kilometraje,
                   Observaciones,
                   Marca,
                   ClienteId,
                   EsPlacaExtranjera,
                   CreadoPor,
                   FechaCreacion,
                   ModificadoPor,
                   FechaModificacion
            FROM Vehiculos
            WHERE Placa LIKE @Filtro ESCAPE '!'
               OR Modelo LIKE @Filtro ESCAPE '!'
               OR Marca LIKE @Filtro ESCAPE '!'
            ORDER BY Placa;
            """;

        using DbCommand command =
            connection.CreateCommand();

        command.CommandText = query;

        AddParameter(
            command,
            "@Filtro",
            $"%{EscaparPatron(filtro)}%");

        using DbDataReader reader =
            command.ExecuteReader();

        while (reader.Read())
        {
            vehiculos.Add(
                MapVehiculo(reader));
        }

        return vehiculos;
    }

    public async Task<IReadOnlyList<SugerenciaVehiculo>> SugerirAsync(
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
                (Marca COLLATE utf8mb4_0900_ai_ci LIKE @Inicio{indice} ESCAPE '!'
                 OR Marca COLLATE utf8mb4_0900_ai_ci LIKE @Palabra{indice} ESCAPE '!'
                 OR Modelo COLLATE utf8mb4_0900_ai_ci LIKE @Inicio{indice} ESCAPE '!'
                 OR Modelo COLLATE utf8mb4_0900_ai_ci LIKE @Palabra{indice} ESCAPE '!')
                """);
            var palabraEscapada = EscaparPatron(palabras[indice]);
            AddParameter(command, $"@Inicio{indice}", $"{palabraEscapada}%");
            AddParameter(command, $"@Palabra{indice}", $"% {palabraEscapada}%");
        }

        command.CommandText = $"""
            SELECT Id, Placa, Marca, Modelo
            FROM Vehiculos
            WHERE Placa COLLATE utf8mb4_0900_ai_ci LIKE @PrefijoPlaca ESCAPE '!'
               OR ({string.Join(" AND ", condicionesPalabras)})
            ORDER BY Placa ASC, Id ASC
            LIMIT @Limite;
            """;
        AddParameter(command, "@PrefijoPlaca", $"{EscaparPatron(termino)}%");
        AddParameter(command, "@Limite", MaximoSugerencias);

        var sugerencias = new List<SugerenciaVehiculo>();
        await using DbDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var marca = reader.IsDBNull(2) ? string.Empty : reader.GetString(2);
            var modelo = reader.GetString(3);
            sugerencias.Add(new SugerenciaVehiculo(
                reader.GetInt32(0), reader.GetString(1),
                string.Join(" · ", new[] { marca, modelo }.Where(texto => !string.IsNullOrWhiteSpace(texto)))));
        }

        return sugerencias;
    }

    public bool ExistsByPlaca(
        string placa,
        int idExcluido = 0)
    {
        using DbConnection connection =
            _connectionFactory.CreateConnection();

        connection.Open();

        const string query = """
            SELECT COUNT(*)
            FROM Vehiculos
            WHERE Placa = @Placa
              AND Id <> @IdExcluido;
            """;

        using DbCommand command =
            connection.CreateCommand();

        command.CommandText = query;

        AddParameter(
            command,
            "@Placa",
            placa);

        AddParameter(
            command,
            "@IdExcluido",
            idExcluido);

        return Convert.ToInt32(
            command.ExecuteScalar()) > 0;
    }

    public bool ExistsPorCliente(
        int clienteId)
    {
        using DbConnection connection =
            _connectionFactory.CreateConnection();

        connection.Open();

        const string query = """
            SELECT COUNT(*)
            FROM Vehiculos
            WHERE ClienteId = @ClienteId;
            """;

        using DbCommand command =
            connection.CreateCommand();

        command.CommandText = query;

        AddParameter(
            command,
            "@ClienteId",
            clienteId);

        return Convert.ToInt32(
            command.ExecuteScalar()) > 0;
    }

    public void Add(
        Vehiculo vehiculo)
    {
        using DbConnection connection =
            _connectionFactory.CreateConnection();

        connection.Open();

        const string query = """
            INSERT INTO Vehiculos
            (
                Placa,
                Modelo,
                Kilometraje,
                Observaciones,
                Marca,
                ClienteId,
                EsPlacaExtranjera,
                CreadoPor,
                FechaCreacion,
                ModificadoPor,
                FechaModificacion
            )
            VALUES
            (
                @Placa,
                @Modelo,
                @Kilometraje,
                @Observaciones,
                @Marca,
                @ClienteId,
                @EsPlacaExtranjera,
                @CreadoPor,
                @FechaCreacion,
                @ModificadoPor,
                @FechaModificacion
            );
            """;

        using DbCommand command =
            connection.CreateCommand();

        command.CommandText = query;

        AddParameters(
            command,
            vehiculo);

        command.ExecuteNonQuery();
    }

    public void Update(
        Vehiculo vehiculo)
    {
        using DbConnection connection =
            _connectionFactory.CreateConnection();

        connection.Open();

        const string query = """
            UPDATE Vehiculos
            SET Placa = @Placa,
                Marca = @Marca,
                Modelo = @Modelo,
                Kilometraje = @Kilometraje,
                Observaciones = @Observaciones,
                ClienteId = @ClienteId,
                EsPlacaExtranjera = @EsPlacaExtranjera,
                ModificadoPor = @ModificadoPor,
                FechaModificacion = @FechaModificacion
            WHERE Id = @Id;
            """;

        using DbCommand command =
            connection.CreateCommand();

        command.CommandText = query;

        AddParameters(
            command,
            vehiculo);

        AddParameter(
            command,
            "@Id",
            vehiculo.Id);

        command.ExecuteNonQuery();
    }

    public void Delete(
        int id)
    {
        using DbConnection connection =
            _connectionFactory.CreateConnection();

        connection.Open();

        const string query = """
            DELETE FROM Vehiculos
            WHERE Id = @Id;
            """;

        using DbCommand command =
            connection.CreateCommand();

        command.CommandText = query;

        AddParameter(
            command,
            "@Id",
            id);

        command.ExecuteNonQuery();
    }

    public int Count()
    {
        using DbConnection connection =
            _connectionFactory.CreateConnection();

        connection.Open();

        const string query = """
            SELECT COUNT(*)
            FROM Vehiculos;
            """;

        using DbCommand command =
            connection.CreateCommand();

        command.CommandText = query;

        return Convert.ToInt32(
            command.ExecuteScalar());
    }

    private static void AddParameters(
        DbCommand command,
        Vehiculo vehiculo)
    {
        AddParameter(
            command,
            "@Placa",
            vehiculo.Placa);

        AddParameter(command, "@EsPlacaExtranjera", vehiculo.EsPlacaExtranjera);

        AddParameter(
            command,
            "@Modelo",
            vehiculo.Modelo);

        AddParameter(
            command,
            "@Marca",
            vehiculo.Marca);

        AddParameter(
            command,
            "@Kilometraje",
            vehiculo.Kilometraje);

        AddParameter(
            command,
            "@Observaciones",
            vehiculo.Observaciones);

        AddParameter(
            command,
            "@ClienteId",
            vehiculo.ClienteId.HasValue
                ? vehiculo.ClienteId.Value
                : DBNull.Value);

        AddParameter(
            command,
            "@CreadoPor",
            vehiculo.CreadoPor);

        AddParameter(
            command,
            "@FechaCreacion",
            vehiculo.FechaCreacion);

        AddParameter(
            command,
            "@ModificadoPor",
            vehiculo.ModificadoPor);

        AddParameter(
            command,
            "@FechaModificacion",
            vehiculo.FechaModificacion);
    }

    private static void AddParameter(
        DbCommand command,
        string nombre,
        object? valor)
    {
        DbParameter parameter =
            command.CreateParameter();

        parameter.ParameterName =
            nombre;

        parameter.Value =
            valor ?? DBNull.Value;

        command.Parameters.Add(
            parameter);
    }

    private static Vehiculo MapVehiculo(
        DbDataReader reader)
    {
        int clienteIdOrdinal =
            reader.GetOrdinal(
                "ClienteId");

        return new Vehiculo
        {
            Id =
                reader.GetInt32(
                    reader.GetOrdinal("Id")),

            Placa =
                reader.GetString(
                    reader.GetOrdinal("Placa")),

            EsPlacaExtranjera = reader.GetBoolean(reader.GetOrdinal("EsPlacaExtranjera")),

            Modelo =
                reader.GetString(
                    reader.GetOrdinal("Modelo")),

            Kilometraje =
                reader.GetInt32(
                    reader.GetOrdinal("Kilometraje")),

            Observaciones =
                reader.GetString(
                    reader.GetOrdinal("Observaciones")),

            Marca =
                reader.GetString(
                    reader.GetOrdinal("Marca")),

            ClienteId =
                reader.IsDBNull(clienteIdOrdinal)
                    ? null
                    : reader.GetInt32(
                        clienteIdOrdinal),

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

    private static string EscaparPatron(
        string filtro)
    {
        return filtro
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
