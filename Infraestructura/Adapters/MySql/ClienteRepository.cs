using System.Data.Common;
using TallerMecanico.Application.Ports;
using TallerMecanico.Data;
using TallerMecanico.Data.Factories;
using TallerMecanico.Models;

namespace TallerMecanico.Infraestructura.Adapters.MySql;

public class ClienteRepository :
    IRepository<Cliente>,
    IClientePort
{
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
                   FechaCreacion
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
                   FechaCreacion
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
                   FechaCreacion
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
                Celular = @Celular
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
                    reader.GetOrdinal("FechaCreacion"))
        };
    }

    private static void AddParameter(
        DbCommand command,
        string nombre,
        object valor)
    {
        DbParameter parameter =
            command.CreateParameter();

        parameter.ParameterName = nombre;
        parameter.Value = valor;

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