using Microsoft.Data.Sqlite;
using TallerMecanico.Models;

namespace TallerMecanico.Data;

public class VehiculoRepository : IVehiculoRepository
{
    private readonly DatabaseConnection _databaseConnection;

    public VehiculoRepository(DatabaseConnection databaseConnection)
    {
        _databaseConnection = databaseConnection;
    }

    public List<Vehiculo> GetAll()
    {
        List<Vehiculo> vehiculos = [];

        using SqliteConnection connection = _databaseConnection.CreateConnection();
        connection.Open();

        const string query = """
            SELECT Id, Placa, Modelo, Kilometraje, Observaciones
            FROM Vehiculos
            ORDER BY Placa;
            """;

        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = query;

        using SqliteDataReader reader = command.ExecuteReader();

        while (reader.Read())
        {
            vehiculos.Add(MapVehiculo(reader));
        }

        return vehiculos;
    }

    public List<Vehiculo> Search(string filtro)
    {
        List<Vehiculo> vehiculos = [];

        using SqliteConnection connection = _databaseConnection.CreateConnection();
        connection.Open();

        const string query = """
            SELECT Id, Placa, Modelo, Kilometraje, Observaciones
            FROM Vehiculos
            WHERE Placa LIKE @Filtro
               OR Modelo LIKE @Filtro
            ORDER BY Placa;
            """;

        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = query;
        command.Parameters.AddWithValue("@Filtro", $"%{filtro}%");

        using SqliteDataReader reader = command.ExecuteReader();

        while (reader.Read())
        {
            vehiculos.Add(MapVehiculo(reader));
        }

        return vehiculos;
    }

    public Vehiculo? GetById(int id)
    {
        using SqliteConnection connection = _databaseConnection.CreateConnection();
        connection.Open();

        const string query = """
            SELECT Id, Placa, Modelo, Kilometraje, Observaciones
            FROM Vehiculos
            WHERE Id = @Id;
            """;

        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = query;
        command.Parameters.AddWithValue("@Id", id);

        using SqliteDataReader reader = command.ExecuteReader();

        if (!reader.Read())
        {
            return null;
        }

        return MapVehiculo(reader);
    }

    public void Add(Vehiculo vehiculo)
    {
        using SqliteConnection connection = _databaseConnection.CreateConnection();
        connection.Open();

        const string query = """
            INSERT INTO Vehiculos (Placa, Modelo, Kilometraje, Observaciones)
            VALUES (@Placa, @Modelo, @Kilometraje, @Observaciones);
            """;

        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = query;
        AddParameters(command, vehiculo);
        command.ExecuteNonQuery();
    }

    public void Update(Vehiculo vehiculo)
    {
        using SqliteConnection connection = _databaseConnection.CreateConnection();
        connection.Open();

        const string query = """
            UPDATE Vehiculos
            SET Placa = @Placa,
                Modelo = @Modelo,
                Kilometraje = @Kilometraje,
                Observaciones = @Observaciones
            WHERE Id = @Id;
            """;

        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = query;
        AddParameters(command, vehiculo);
        command.Parameters.AddWithValue("@Id", vehiculo.Id);
        command.ExecuteNonQuery();
    }

    public void Delete(int id)
    {
        using SqliteConnection connection = _databaseConnection.CreateConnection();
        connection.Open();

        const string query = """
            DELETE FROM Vehiculos
            WHERE Id = @Id;
            """;

        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = query;
        command.Parameters.AddWithValue("@Id", id);
        command.ExecuteNonQuery();
    }

    public bool ExistsByPlaca(string placa, int idExcluido)
    {
        using SqliteConnection connection = _databaseConnection.CreateConnection();
        connection.Open();

        const string query = """
            SELECT COUNT(*)
            FROM Vehiculos
            WHERE Placa = @Placa
              AND Id <> @IdExcluido;
            """;

        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = query;
        command.Parameters.AddWithValue("@Placa", placa);
        command.Parameters.AddWithValue("@IdExcluido", idExcluido);

        return Convert.ToInt32(command.ExecuteScalar()) > 0;
    }

    public int Count()
    {
        using SqliteConnection connection = _databaseConnection.CreateConnection();
        connection.Open();

        const string query = """
            SELECT COUNT(*)
            FROM Vehiculos;
            """;

        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = query;

        return Convert.ToInt32(command.ExecuteScalar());
    }

    private static void AddParameters(SqliteCommand command, Vehiculo vehiculo)
    {
        command.Parameters.AddWithValue("@Placa", vehiculo.Placa);
        command.Parameters.AddWithValue("@Modelo", vehiculo.Modelo);
        command.Parameters.AddWithValue("@Kilometraje", vehiculo.Kilometraje);
        command.Parameters.AddWithValue("@Observaciones", vehiculo.Observaciones);
    }

    private static Vehiculo MapVehiculo(SqliteDataReader reader)
    {
        return new Vehiculo
        {
            Id = reader.GetInt32(0),
            Placa = reader.GetString(1),
            Modelo = reader.GetString(2),
            Kilometraje = reader.GetInt32(3),
            Observaciones = reader.GetString(4)
        };
    }
}
