using System.Data.Common;
using TallerMecanico.Data.Factories;
using TallerMecanico.Models;

namespace TallerMecanico.Data;

public class VehiculoRepository : IRepository<Vehiculo>
{
    private readonly DatabaseConnectionFactory _connectionFactory;

    public VehiculoRepository(DatabaseConnectionFactory connectionFactory)
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
            SELECT Id, Placa, Modelo, Kilometraje, Observaciones, Marca
            FROM Vehiculos
            ORDER BY Placa;
            """;

        using DbCommand command = connection.CreateCommand();
        command.CommandText = query;

        using DbDataReader reader = command.ExecuteReader();

        while (reader.Read())
        {
            vehiculos.Add(MapVehiculo(reader));
        }

        return vehiculos;
    }

    public List<Vehiculo> Search(string filtro)
    {
        List<Vehiculo> vehiculos = [];

        using DbConnection connection =
            _connectionFactory.CreateConnection();

        connection.Open();

        const string query = """
            SELECT Id, Placa, Modelo, Kilometraje, Observaciones, Marca
            FROM Vehiculos
            WHERE Placa LIKE @Filtro ESCAPE '!'
               OR Modelo LIKE @Filtro ESCAPE '!'
               OR Marca LIKE @Filtro ESCAPE '!'
            ORDER BY Placa;
            """;

        using DbCommand command = connection.CreateCommand();
        command.CommandText = query;
        AddParameter(command, "@Filtro", $"%{EscaparPatron(filtro)}%");

        using DbDataReader reader = command.ExecuteReader();

        while (reader.Read())
        {
            vehiculos.Add(MapVehiculo(reader));
        }

        return vehiculos;
    }

    public Vehiculo? GetById(int id)
    {
        using DbConnection connection =
            _connectionFactory.CreateConnection();

        connection.Open();

        const string query = """
            SELECT Id, Placa, Modelo, Kilometraje, Observaciones, Marca
            FROM Vehiculos
            WHERE Id = @Id;
            """;

        using DbCommand command = connection.CreateCommand();
        command.CommandText = query;
        AddParameter(command, "@Id", id);

        using DbDataReader reader = command.ExecuteReader();

        if (!reader.Read())
        {
            return null;
        }

        return MapVehiculo(reader);
    }

    public void Add(Vehiculo vehiculo)
    {
        using DbConnection connection =
            _connectionFactory.CreateConnection();

        connection.Open();

        const string query = """
            INSERT INTO Vehiculos (Placa, Modelo, Kilometraje, Observaciones, Marca)
            VALUES (@Placa, @Modelo, @Kilometraje, @Observaciones, @Marca);
            """;

        using DbCommand command = connection.CreateCommand();
        command.CommandText = query;
        AddParameters(command, vehiculo);
        command.ExecuteNonQuery();
    }

    public void Update(Vehiculo vehiculo)
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
                Observaciones = @Observaciones
            WHERE Id = @Id;
            """;

        using DbCommand command = connection.CreateCommand();
        command.CommandText = query;
        AddParameters(command, vehiculo);
        AddParameter(command, "@Id", vehiculo.Id);
        command.ExecuteNonQuery();
    }

    public void Delete(int id)
    {
        using DbConnection connection =
            _connectionFactory.CreateConnection();

        connection.Open();

        const string query = """
            DELETE FROM Vehiculos
            WHERE Id = @Id;
            """;

        using DbCommand command = connection.CreateCommand();
        command.CommandText = query;
        AddParameter(command, "@Id", id);
        command.ExecuteNonQuery();
    }

    public bool ExistsByPlaca(string placa, int idExcluido)
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

        using DbCommand command = connection.CreateCommand();
        command.CommandText = query;
        AddParameter(command, "@Placa", placa);
        AddParameter(command, "@IdExcluido", idExcluido);

        return Convert.ToInt32(command.ExecuteScalar()) > 0;
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

        using DbCommand command = connection.CreateCommand();
        command.CommandText = query;

        return Convert.ToInt32(command.ExecuteScalar());
    }

    private static void AddParameters(DbCommand command, Vehiculo vehiculo)
    {
        AddParameter(command, "@Placa", vehiculo.Placa);
        AddParameter(command, "@Modelo", vehiculo.Modelo);
        AddParameter(command, "@Marca", vehiculo.Marca);
        AddParameter(command, "@Kilometraje", vehiculo.Kilometraje);
        AddParameter(command, "@Observaciones", vehiculo.Observaciones);
    }

    private static void AddParameter(
        DbCommand command,
        string nombre,
        object valor)
    {
        DbParameter parameter = command.CreateParameter();

        parameter.ParameterName = nombre;
        parameter.Value = valor;

        command.Parameters.Add(parameter);
    }

    private static Vehiculo MapVehiculo(DbDataReader reader)
    {
        return new Vehiculo
        {
            Id = reader.GetInt32(0),
            Placa = reader.GetString(1),
            Modelo = reader.GetString(2),
            Kilometraje = reader.GetInt32(3),
            Observaciones = reader.GetString(4),
            Marca = reader.GetString(5)
        };
    }

    private static string EscaparPatron(string filtro)
    {
        return filtro
            .Trim()
            .Replace("!", "!!", StringComparison.Ordinal)
            .Replace("%", "!%", StringComparison.Ordinal)
            .Replace("_", "!_", StringComparison.Ordinal);
    }
}
