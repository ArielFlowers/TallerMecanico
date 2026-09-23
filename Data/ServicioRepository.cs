using System.Data.Common;
using TallerMecanico.Data.Factories;
using TallerMecanico.Models;

namespace TallerMecanico.Data;

public class ServicioRepository : IRepository<Servicio>
{
    private readonly DatabaseConnectionFactory _connectionFactory;

    public ServicioRepository(DatabaseConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public List<Servicio> GetAll()
    {
        List<Servicio> servicios = [];

        using DbConnection connection =
            _connectionFactory.CreateConnection();

        connection.Open();

        const string query = """
            SELECT Id, Nombre, Descripcion, Costo, TiempoEstimadoHoras
            FROM Servicios
            ORDER BY Nombre;
            """;

        using DbCommand command = connection.CreateCommand();
        command.CommandText = query;

        using DbDataReader reader = command.ExecuteReader();

        while (reader.Read())
        {
            servicios.Add(MapServicio(reader));
        }

        return servicios;
    }

    public Servicio? GetById(int id)
    {
        using DbConnection connection =
            _connectionFactory.CreateConnection();

        connection.Open();

        const string query = """
            SELECT Id, Nombre, Descripcion, Costo, TiempoEstimadoHoras
            FROM Servicios
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

        return MapServicio(reader);
    }

    public void Add(Servicio servicio)
    {
        using DbConnection connection =
            _connectionFactory.CreateConnection();

        connection.Open();

        const string query = """
            INSERT INTO Servicios
                (Nombre, Descripcion, Costo, TiempoEstimadoHoras)
            VALUES
                (@Nombre, @Descripcion, @Costo, @TiempoEstimadoHoras);
            """;

        using DbCommand command = connection.CreateCommand();
        command.CommandText = query;

        AddParameters(command, servicio);

        command.ExecuteNonQuery();
    }

    public void Update(Servicio servicio)
    {
        using DbConnection connection =
            _connectionFactory.CreateConnection();

        connection.Open();

        const string query = """
            UPDATE Servicios
            SET Nombre = @Nombre,
                Descripcion = @Descripcion,
                Costo = @Costo,
                TiempoEstimadoHoras = @TiempoEstimadoHoras
            WHERE Id = @Id;
            """;

        using DbCommand command = connection.CreateCommand();
        command.CommandText = query;

        AddParameters(command, servicio);
        AddParameter(command, "@Id", servicio.Id);

        command.ExecuteNonQuery();
    }

    public void Delete(int id)
    {
        using DbConnection connection =
            _connectionFactory.CreateConnection();

        connection.Open();

        const string query = """
            DELETE FROM Servicios
            WHERE Id = @Id;
            """;

        using DbCommand command = connection.CreateCommand();
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
            FROM Servicios;
            """;

        using DbCommand command = connection.CreateCommand();
        command.CommandText = query;

        return Convert.ToInt32(command.ExecuteScalar());
    }

    private static void AddParameters(
        DbCommand command,
        Servicio servicio)
    {
        AddParameter(command, "@Nombre", servicio.Nombre);
        AddParameter(command, "@Descripcion", servicio.Descripcion);
        AddParameter(command, "@Costo", servicio.Costo);
        AddParameter(
            command,
            "@TiempoEstimadoHoras",
            servicio.TiempoEstimadoHoras);
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

    private static Servicio MapServicio(DbDataReader reader)
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
