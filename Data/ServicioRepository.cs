using Microsoft.Data.Sqlite;
using TallerMecanico.Models;

namespace TallerMecanico.Data;

public class ServicioRepository : IServicioRepository
{
    private readonly DatabaseConnection _databaseConnection;

    public ServicioRepository(DatabaseConnection databaseConnection)
    {
        _databaseConnection = databaseConnection;
    }

    public List<Servicio> GetAll()
    {
        List<Servicio> servicios = [];

        using SqliteConnection connection =
            _databaseConnection.CreateConnection();

        connection.Open();

        const string query = """
            SELECT Id, Nombre, Descripcion, Costo, TiempoEstimadoHoras
            FROM Servicios
            ORDER BY Nombre;
            """;

        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = query;

        using SqliteDataReader reader = command.ExecuteReader();

        while (reader.Read())
        {
            servicios.Add(MapServicio(reader));
        }

        return servicios;
    }

    public Servicio? GetById(int id)
    {
        using SqliteConnection connection =
            _databaseConnection.CreateConnection();

        connection.Open();

        const string query = """
            SELECT Id, Nombre, Descripcion, Costo, TiempoEstimadoHoras
            FROM Servicios
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

        return MapServicio(reader);
    }

    public void Add(Servicio servicio)
    {
        using SqliteConnection connection =
            _databaseConnection.CreateConnection();

        connection.Open();

        const string query = """
            INSERT INTO Servicios
                (Nombre, Descripcion, Costo, TiempoEstimadoHoras)
            VALUES
                (@Nombre, @Descripcion, @Costo, @TiempoEstimadoHoras);
            """;

        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = query;

        AddParameters(command, servicio);

        command.ExecuteNonQuery();
    }

    public void Update(Servicio servicio)
    {
        using SqliteConnection connection =
            _databaseConnection.CreateConnection();

        connection.Open();

        const string query = """
            UPDATE Servicios
            SET Nombre = @Nombre,
                Descripcion = @Descripcion,
                Costo = @Costo,
                TiempoEstimadoHoras = @TiempoEstimadoHoras
            WHERE Id = @Id;
            """;

        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = query;

        AddParameters(command, servicio);
        command.Parameters.AddWithValue("@Id", servicio.Id);

        command.ExecuteNonQuery();
    }

    public void Delete(int id)
    {
        using SqliteConnection connection =
            _databaseConnection.CreateConnection();

        connection.Open();

        const string query = """
            DELETE FROM Servicios
            WHERE Id = @Id;
            """;

        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = query;
        command.Parameters.AddWithValue("@Id", id);

        command.ExecuteNonQuery();
    }

    public int Count()
    {
        using SqliteConnection connection =
            _databaseConnection.CreateConnection();

        connection.Open();

        const string query = """
            SELECT COUNT(*)
            FROM Servicios;
            """;

        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = query;

        return Convert.ToInt32(command.ExecuteScalar());
    }

    private static void AddParameters(
        SqliteCommand command,
        Servicio servicio)
    {
        command.Parameters.AddWithValue(
            "@Nombre",
            servicio.Nombre);

        command.Parameters.AddWithValue(
            "@Descripcion",
            servicio.Descripcion);

        command.Parameters.AddWithValue(
            "@Costo",
            servicio.Costo);

        command.Parameters.AddWithValue(
            "@TiempoEstimadoHoras",
            servicio.TiempoEstimadoHoras);
    }

    private static Servicio MapServicio(SqliteDataReader reader)
    {
        return new Servicio
        {
            Id = reader.GetInt32(0),
            Nombre = reader.GetString(1),
            Descripcion = reader.GetString(2),
            Costo = reader.GetDecimal(3),
            TiempoEstimadoHoras = reader.GetDecimal(4)
        };
    }
}
