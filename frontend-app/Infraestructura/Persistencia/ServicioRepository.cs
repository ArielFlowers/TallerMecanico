using System.Data.Common;
using TallerMecanico.Aplicacion.Dto;
using TallerMecanico.Aplicacion.Puertos;
using TallerMecanico.Infraestructura.Persistencia;
using TallerMecanico.Dominio.Entidades;

namespace TallerMecanico.Infraestructura.Persistencia;

public class ServicioRepository : IRepository<Servicio>, IConsultaServiciosPort
{
    private const int MaximoSugerencias = 10;
    private const int LongitudDescripcionBreve = 100;
    private readonly DatabaseConnectionFactory _connectionFactory;

    public ServicioRepository(DatabaseConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public List<Servicio> GetAll() => ObtenerServicios(false);

    public List<Servicio> ObtenerCatalogo() => ObtenerServicios(true);

    private List<Servicio> ObtenerServicios(bool soloCatalogo)
    {
        List<Servicio> servicios = [];

        using DbConnection connection =
            _connectionFactory.CreateConnection();

        connection.Open();

        using DbCommand command = connection.CreateCommand();
        command.CommandText = $"""
            SELECT Id, Nombre, Descripcion, Costo, TiempoEstimadoHoras, CreadoPor, FechaCreacion, ModificadoPor, FechaModificacion, UnidadCantidad, CodigoCatalogo
            FROM Servicios
            WHERE {(soloCatalogo ? FiltroCatalogo(command) : "1 = 1")}
            ORDER BY Nombre, Id;
            """;

        using DbDataReader reader = command.ExecuteReader();

        while (reader.Read())
        {
            servicios.Add(MapServicio(reader));
        }

        return servicios;
    }

    public Servicio? GetById(int id) => ObtenerServicio(id, false);

    public Servicio? ObtenerDelCatalogo(int id) => ObtenerServicio(id, true);

    private Servicio? ObtenerServicio(int id, bool soloCatalogo)
    {
        using DbConnection connection =
            _connectionFactory.CreateConnection();

        connection.Open();

        using DbCommand command = connection.CreateCommand();
        command.CommandText = $"""
            SELECT Id, Nombre, Descripcion, Costo, TiempoEstimadoHoras, CreadoPor, FechaCreacion, ModificadoPor, FechaModificacion, UnidadCantidad, CodigoCatalogo
            FROM Servicios
            WHERE Id = @Id AND {(soloCatalogo ? FiltroCatalogo(command) : "1 = 1")};
            """;

        AddParameter(command, "@Id", id);

        using DbDataReader reader = command.ExecuteReader();

        if (!reader.Read())
        {
            return null;
        }

        return MapServicio(reader);
    }

    public async Task<IReadOnlyList<Servicio>> BuscarAsync(
        string termino, CancellationToken cancellationToken = default)
    {
        await using DbConnection connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using DbCommand command = connection.CreateCommand();
        command.CommandText = """
            SELECT Id, Nombre, Descripcion, Costo, TiempoEstimadoHoras,
                   CreadoPor, FechaCreacion, ModificadoPor, FechaModificacion, UnidadCantidad, CodigoCatalogo
            FROM Servicios
            WHERE Nombre COLLATE utf8mb4_0900_ai_ci LIKE @Filtro ESCAPE '!'
               OR Descripcion COLLATE utf8mb4_0900_ai_ci LIKE @Filtro ESCAPE '!'
            ORDER BY Nombre, Id;
            """;
        AddParameter(command, "@Filtro", $"%{EscaparPatron(termino)}%");

        var servicios = new List<Servicio>();
        await using DbDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            servicios.Add(MapServicio(reader));
        }

        return servicios;
    }

    public Task<IReadOnlyList<SugerenciaServicio>> SugerirAsync(
        string termino, CancellationToken cancellationToken = default) =>
        SugerirAsync(termino, false, cancellationToken);

    public Task<IReadOnlyList<SugerenciaServicio>> SugerirCatalogoAsync(
        string termino, CancellationToken cancellationToken = default) =>
        SugerirAsync(termino, true, cancellationToken);

    private async Task<IReadOnlyList<SugerenciaServicio>> SugerirAsync(
        string termino, bool soloCatalogo, CancellationToken cancellationToken)
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
                (Nombre COLLATE utf8mb4_0900_ai_ci LIKE @Inicio{indice} ESCAPE '!'
                 OR Nombre COLLATE utf8mb4_0900_ai_ci LIKE @Palabra{indice} ESCAPE '!'
                 OR Descripcion COLLATE utf8mb4_0900_ai_ci LIKE @Inicio{indice} ESCAPE '!'
                 OR Descripcion COLLATE utf8mb4_0900_ai_ci LIKE @Palabra{indice} ESCAPE '!')
                """);
            string palabraEscapada = EscaparPatron(palabras[indice]);
            AddParameter(command, $"@Inicio{indice}", $"{palabraEscapada}%");
            AddParameter(command, $"@Palabra{indice}", $"% {palabraEscapada}%");
        }

        command.CommandText = $"""
            SELECT Id, Nombre,
                   CASE WHEN CHAR_LENGTH(Descripcion) > @LongitudDescripcion
                        THEN CONCAT(LEFT(Descripcion, @LongitudDescripcion), '…')
                        ELSE Descripcion END
            FROM Servicios
            WHERE ({(soloCatalogo ? FiltroCatalogo(command) : "1 = 1")})
              AND {string.Join(" AND ", condicionesPalabras)}
            ORDER BY Nombre, Id
            LIMIT @Limite;
            """;
        AddParameter(command, "@LongitudDescripcion", LongitudDescripcionBreve);
        AddParameter(command, "@Limite", MaximoSugerencias);

        var sugerencias = new List<SugerenciaServicio>();
        await using DbDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            sugerencias.Add(new SugerenciaServicio(reader.GetInt32(0), reader.GetString(1), reader.GetString(2)));
        }

        return sugerencias;
    }

    private static string EscaparPatron(string termino)
    {
        return termino.Replace("!", "!!").Replace("%", "!%").Replace("_", "!_");
    }

    private static string FiltroCatalogo(DbCommand comando)
    {
        var condiciones = new List<string>();
        for (int indice = 0; indice < CatalogoServicios.Servicios.Count; indice++)
        {
            ServicioCatalogo servicio = CatalogoServicios.Servicios[indice];
            condiciones.Add($"(CodigoCatalogo = @Codigo{indice} AND UnidadCantidad <=> @Unidad{indice})");
            AddParameter(comando, $"@Codigo{indice}", servicio.Codigo);
            AddParameter(comando, $"@Unidad{indice}", servicio.UnidadCantidad);
        }
        return $"({string.Join(" OR ", condiciones)})";
    }

    public void Add(Servicio servicio)
    {
        using DbConnection connection =
            _connectionFactory.CreateConnection();

        connection.Open();

        const string query = """
            INSERT INTO Servicios
                (Nombre, Descripcion, Costo, TiempoEstimadoHoras, CreadoPor, FechaCreacion, ModificadoPor, FechaModificacion, UnidadCantidad)
            VALUES
                (@Nombre, @Descripcion, @Costo, @TiempoEstimadoHoras, @CreadoPor, @FechaCreacion, @ModificadoPor, @FechaModificacion, @UnidadCantidad);
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
                TiempoEstimadoHoras = @TiempoEstimadoHoras,
                ModificadoPor = @ModificadoPor,
                FechaModificacion = @FechaModificacion,
                UnidadCantidad = @UnidadCantidad
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
        AddParameter(command, "@CreadoPor", servicio.CreadoPor);
        AddParameter(command, "@FechaCreacion", servicio.FechaCreacion);
        AddParameter(command, "@ModificadoPor", servicio.ModificadoPor);
        AddParameter(command, "@FechaModificacion", servicio.FechaModificacion);
        AddParameter(command, "@UnidadCantidad", servicio.UnidadCantidad);
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

    private static Servicio MapServicio(DbDataReader reader)
    {
        return new Servicio
        {
            Id = reader.GetInt32(0),
            Nombre = reader.GetString(1),
            Descripcion = reader.GetString(2),
            Costo = reader.GetDecimal(3),
            TiempoEstimadoHoras = reader.GetDecimal(4),
            CreadoPor = reader.GetString(5),
            FechaCreacion = reader.GetDateTime(6),
            ModificadoPor = reader.IsDBNull(7) ? null : reader.GetString(7),
            FechaModificacion = reader.IsDBNull(8) ? null : reader.GetDateTime(8),
            UnidadCantidad = reader.IsDBNull(9) ? null : reader.GetString(9),
            CodigoCatalogo = reader.IsDBNull(10) ? null : reader.GetString(10)
        };
    }
}
