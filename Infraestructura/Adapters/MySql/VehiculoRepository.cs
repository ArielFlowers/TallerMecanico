using System.Data.Common;
using TallerMecanico.Application.Ports;
using TallerMecanico.Data;
using TallerMecanico.Data.Factories;
using TallerMecanico.Models;

namespace TallerMecanico.Infraestructura.Adapters.MySql;

public class VehiculoRepository :
    IRepository<Vehiculo>,
    IVehiculoPort
{
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
                   EsPlacaExtranjera
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
                   EsPlacaExtranjera
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
                   EsPlacaExtranjera
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
                EsPlacaExtranjera
            )
            VALUES
            (
                @Placa,
                @Modelo,
                @Kilometraje,
                @Observaciones,
                @Marca,
                @ClienteId,
                @EsPlacaExtranjera
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
                EsPlacaExtranjera = @EsPlacaExtranjera
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
    }

    private static void AddParameter(
        DbCommand command,
        string nombre,
        object valor)
    {
        DbParameter parameter =
            command.CreateParameter();

        parameter.ParameterName =
            nombre;

        parameter.Value =
            valor;

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
                        clienteIdOrdinal)
        };
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
