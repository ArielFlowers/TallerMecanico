using System.Data.Common;
using TallerMecanico.Data.Factories;
using TallerMecanico.Models;

namespace TallerMecanico.Data;

public class MecanicoRepository : IRepository<Mecanico>
{
    private readonly DatabaseConnectionFactory _connectionFactory;

    public MecanicoRepository(DatabaseConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public List<Mecanico> GetAll()
    {
        List<Mecanico> mecanicos = [];

        using DbConnection connection =
            _connectionFactory.CreateConnection();

        connection.Open();

        const string query = """
            SELECT Id,
                   Ci,
                   Nombres,
                   Apellidos,
                   Genero,
                   Especialidad,
                   Celular
            FROM Mecanicos
            ORDER BY Apellidos ASC,
                     Nombres ASC,
                     Ci ASC;
            """;

        using DbCommand command = connection.CreateCommand();
        command.CommandText = query;

        using DbDataReader reader = command.ExecuteReader();

        while (reader.Read())
        {
            mecanicos.Add(MapMecanico(reader));
        }

        return mecanicos;
    }

    public List<Mecanico> Search(string terminoBusqueda)
    {
        List<Mecanico> mecanicos = [];

        using DbConnection connection =
            _connectionFactory.CreateConnection();

        connection.Open();

        const string query = """
            SELECT Id,
                   Ci,
                   Nombres,
                   Apellidos,
                   Genero,
                   Especialidad,
                   Celular
            FROM Mecanicos
            WHERE Ci LIKE @PatronBusqueda ESCAPE '!'
               OR Nombres LIKE @PatronBusqueda ESCAPE '!'
               OR Apellidos LIKE @PatronBusqueda ESCAPE '!'
               OR Genero LIKE @PatronBusqueda ESCAPE '!'
               OR Especialidad LIKE @PatronBusqueda ESCAPE '!'
               OR Celular LIKE @PatronBusqueda ESCAPE '!'
            ORDER BY Apellidos ASC,
                     Nombres ASC,
                     Ci ASC;
            """;

        using DbCommand command = connection.CreateCommand();
        command.CommandText = query;
        AddParameter(
            command,
            "@PatronBusqueda",
            $"%{EscaparPatron(terminoBusqueda)}%");

        using DbDataReader reader = command.ExecuteReader();

        while (reader.Read())
        {
            mecanicos.Add(MapMecanico(reader));
        }

        return mecanicos;
    }

    public Mecanico? GetById(int id)
    {
        using DbConnection connection =
            _connectionFactory.CreateConnection();

        connection.Open();

        const string query = """
            SELECT Id,
                   Ci,
                   Nombres,
                   Apellidos,
                   Genero,
                   Especialidad,
                   Celular
            FROM Mecanicos
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

        return MapMecanico(reader);
    }

    public void Add(Mecanico mecanico)
    {
        using DbConnection connection =
            _connectionFactory.CreateConnection();

        connection.Open();

        const string query = """
            INSERT INTO Mecanicos
            (
                Ci,
                Nombres,
                Apellidos,
                Genero,
                Especialidad,
                Celular
            )
            VALUES
            (
                @Ci,
                @Nombres,
                @Apellidos,
                @Genero,
                @Especialidad,
                @Celular
            );
            """;

        using DbCommand command = connection.CreateCommand();
        command.CommandText = query;
        AddParameters(command, mecanico);
        command.ExecuteNonQuery();
    }

    public void Update(Mecanico mecanico)
    {
        using DbConnection connection =
            _connectionFactory.CreateConnection();

        connection.Open();

        const string query = """
            UPDATE Mecanicos
            SET Ci = @Ci,
                Nombres = @Nombres,
                Apellidos = @Apellidos,
                Genero = @Genero,
                Especialidad = @Especialidad,
                Celular = @Celular
            WHERE Id = @Id;
            """;

        using DbCommand command = connection.CreateCommand();
        command.CommandText = query;
        AddParameters(command, mecanico);
        AddParameter(command, "@Id", mecanico.Id);
        command.ExecuteNonQuery();
    }

    public void Delete(int id)
    {
        using DbConnection connection =
            _connectionFactory.CreateConnection();

        connection.Open();

        const string query = """
            DELETE FROM Mecanicos
            WHERE Id = @Id;
            """;

        using DbCommand command = connection.CreateCommand();
        command.CommandText = query;
        AddParameter(command, "@Id", id);
        command.ExecuteNonQuery();
    }

    public bool ExistsByCi(string ci, int idExcluido = 0)
    {
        using DbConnection connection =
            _connectionFactory.CreateConnection();

        connection.Open();

        const string query = """
            SELECT COUNT(*)
            FROM Mecanicos
            WHERE Ci = @Ci
              AND Id <> @IdExcluido;
            """;

        using DbCommand command = connection.CreateCommand();
        command.CommandText = query;
        AddParameter(command, "@Ci", ci);
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
            FROM Mecanicos;
            """;

        using DbCommand command = connection.CreateCommand();
        command.CommandText = query;

        return Convert.ToInt32(command.ExecuteScalar());
    }

    private static void AddParameters(DbCommand command, Mecanico mecanico)
    {
        AddParameter(command, "@Ci", mecanico.Ci);
        AddParameter(command, "@Nombres", mecanico.Nombres);
        AddParameter(command, "@Apellidos", mecanico.Apellidos);
        AddParameter(command, "@Genero", mecanico.Genero);
        AddParameter(command, "@Especialidad", mecanico.Especialidad);
        AddParameter(command, "@Celular", mecanico.Celular);
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

    private static Mecanico MapMecanico(DbDataReader reader)
    {
        return new Mecanico
        {
            Id = reader.GetInt32(0),
            Ci = reader.GetString(1),
            Nombres = reader.GetString(2),
            Apellidos = reader.GetString(3),
            Genero = reader.GetString(4),
            Especialidad = reader.GetString(5),
            Celular = reader.GetString(6)
        };
    }

    private static string EscaparPatron(string terminoBusqueda)
    {
        return terminoBusqueda
            .Trim()
            .Replace("!", "!!", StringComparison.Ordinal)
            .Replace("%", "!%", StringComparison.Ordinal)
            .Replace("_", "!_", StringComparison.Ordinal);
    }
}
