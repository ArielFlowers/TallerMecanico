using System.Data.Common;
using TallerMecanico.Aplicacion.Dto;
using TallerMecanico.Aplicacion.Puertos;
using TallerMecanico.Infraestructura.Persistencia;
using TallerMecanico.Dominio.Entidades;

namespace TallerMecanico.Infraestructura.Persistencia;

public class MecanicoRepository : IRepository<Mecanico>, IConsultaMecanicosPort
{
    private const int MaximoSugerencias = 10;
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
                   ComplementoCi,
                   Nombres,
                   PrimerApellido,
                   SegundoApellido,
                   Genero,
                   Especialidad,
                   Celular,
                   CreadoPor,
                   FechaCreacion,
                   ModificadoPor,
                   FechaModificacion
            FROM Mecanicos
            ORDER BY PrimerApellido ASC,
                     SegundoApellido ASC,
                     Nombres ASC,
                     Ci ASC,
                     ComplementoCi ASC;
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
                   ComplementoCi,
                   Nombres,
                   PrimerApellido,
                   SegundoApellido,
                   Genero,
                   Especialidad,
                   Celular,
                   CreadoPor,
                   FechaCreacion,
                   ModificadoPor,
                   FechaModificacion
            FROM Mecanicos
            WHERE Ci LIKE @PatronBusqueda ESCAPE '!'
               OR ComplementoCi LIKE @PatronBusqueda ESCAPE '!'
               OR CONCAT(Ci, '-', ComplementoCi) LIKE @PatronBusqueda ESCAPE '!'
               OR Nombres LIKE @PatronBusqueda ESCAPE '!'
               OR PrimerApellido LIKE @PatronBusqueda ESCAPE '!'
               OR SegundoApellido LIKE @PatronBusqueda ESCAPE '!'
               OR Genero LIKE @PatronBusqueda ESCAPE '!'
               OR Especialidad LIKE @PatronBusqueda ESCAPE '!'
               OR Celular LIKE @PatronBusqueda ESCAPE '!'
            ORDER BY PrimerApellido ASC,
                     SegundoApellido ASC,
                     Nombres ASC,
                     Ci ASC,
                     ComplementoCi ASC;
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

    public async Task<IReadOnlyList<SugerenciaMecanico>> SugerirAsync(
        string termino, CancellationToken cancellationToken = default, bool incluirEspecialidad = true)
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
            SELECT Id, Ci, ComplementoCi, Nombres, PrimerApellido, SegundoApellido, Especialidad
            FROM Mecanicos
            WHERE Ci LIKE @PrefijoCi ESCAPE '!'
               OR ({string.Join(" AND ", condicionesPalabras)})
            ORDER BY PrimerApellido ASC, SegundoApellido ASC, Nombres ASC,
                     Ci ASC, ComplementoCi ASC, Id ASC
            LIMIT @Limite;
            """;
        AddParameter(command, "@PrefijoCi", $"{EscaparPatron(termino)}%");
        AddParameter(command, "@Limite", MaximoSugerencias);

        var sugerencias = new List<SugerenciaMecanico>();
        await using DbDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            sugerencias.Add(MapSugerencia(reader, incluirEspecialidad));
        }

        return sugerencias;
    }

    private static SugerenciaMecanico MapSugerencia(DbDataReader reader, bool incluirEspecialidad)
    {
        var nombres = new[]
        {
            reader.GetString(3), reader.GetString(4),
            reader.IsDBNull(5) ? string.Empty : reader.GetString(5)
        };
        var ci = reader.GetString(1);
        var complemento = reader.GetString(2);
        var ciCompleto = string.IsNullOrEmpty(complemento) ? ci : $"{ci}-{complemento}";
        return new SugerenciaMecanico(
            reader.GetInt32(0),
            string.Join(" ", nombres.Where(nombre => !string.IsNullOrWhiteSpace(nombre))),
            incluirEspecialidad ? $"CI: {ciCompleto} · {reader.GetString(6)}" : $"CI: {ciCompleto}");
    }

    public Mecanico? GetById(int id)
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
                   Genero,
                   Especialidad,
                   Celular,
                   CreadoPor,
                   FechaCreacion,
                   ModificadoPor,
                   FechaModificacion
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
                ComplementoCi,
                Nombres,
                PrimerApellido,
                SegundoApellido,
                Genero,
                Especialidad,
                Celular,
                CreadoPor,
                FechaCreacion,
                ModificadoPor,
                FechaModificacion
            )
            VALUES
            (
                @Ci,
                @ComplementoCi,
                @Nombres,
                @PrimerApellido,
                @SegundoApellido,
                @Genero,
                @Especialidad,
                @Celular,
                @CreadoPor,
                @FechaCreacion,
                @ModificadoPor,
                @FechaModificacion
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
                ComplementoCi = @ComplementoCi,
                Nombres = @Nombres,
                PrimerApellido = @PrimerApellido,
                SegundoApellido = @SegundoApellido,
                Genero = @Genero,
                Especialidad = @Especialidad,
                Celular = @Celular,
                ModificadoPor = @ModificadoPor,
                FechaModificacion = @FechaModificacion
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
            FROM Mecanicos
            WHERE Ci = @Ci
              AND ComplementoCi = @ComplementoCi
              AND Id <> @IdExcluido;
            """;

        using DbCommand command = connection.CreateCommand();
        command.CommandText = query;
        AddParameter(command, "@Ci", ci);
        AddParameter(command, "@ComplementoCi", complementoCi);
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
        AddParameter(command, "@ComplementoCi", mecanico.ComplementoCi);
        AddParameter(command, "@Nombres", mecanico.Nombres);
        AddParameter(command, "@PrimerApellido", mecanico.PrimerApellido);
        AddParameter(command, "@SegundoApellido", mecanico.SegundoApellido ?? string.Empty);
        AddParameter(command, "@Genero", mecanico.Genero);
        AddParameter(command, "@Especialidad", mecanico.Especialidad);
        AddParameter(command, "@Celular", mecanico.Celular);
        AddParameter(command, "@CreadoPor", mecanico.CreadoPor);
        AddParameter(command, "@FechaCreacion", mecanico.FechaCreacion);
        AddParameter(command, "@ModificadoPor", mecanico.ModificadoPor);
        AddParameter(command, "@FechaModificacion", mecanico.FechaModificacion);
    }

    private static void AddParameter(
        DbCommand command,
        string nombre,
        object? valor)
    {
        DbParameter parameter = command.CreateParameter();

        parameter.ParameterName = nombre;
        parameter.Value = valor ?? DBNull.Value;

        command.Parameters.Add(parameter);
    }

    private static Mecanico MapMecanico(DbDataReader reader)
    {
        return new Mecanico
        {
            Id = reader.GetInt32(0),
            Ci = reader.GetString(1),
            ComplementoCi = reader.GetString(2),
            Nombres = reader.GetString(3),
            PrimerApellido = reader.GetString(4),
            SegundoApellido = reader.IsDBNull(5) ? string.Empty : reader.GetString(5),
            Genero = reader.GetString(6),
            Especialidad = reader.GetString(7),
            Celular = reader.GetString(8),
            CreadoPor = reader.GetString(9),
            FechaCreacion = reader.GetDateTime(10),
            ModificadoPor = reader.IsDBNull(11) ? null : reader.GetString(11),
            FechaModificacion = reader.IsDBNull(12) ? null : reader.GetDateTime(12)
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
