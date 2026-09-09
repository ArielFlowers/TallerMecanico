using Microsoft.Data.Sqlite;
using TallerMecanico.Models;

namespace TallerMecanico.Data;

public class MecanicoRepository : IMecanicoRepository
{
    private const string CrearMecanicoSql = """
        INSERT INTO Mecanicos
        (
            Ci,
            NombreCompleto,
            Especialidad,
            Celular
        )
        VALUES
        (
            @Ci,
            @NombreCompleto,
            @Especialidad,
            @Celular
        )
        RETURNING Id;
        """;

    private const string ObtenerMecanicosSql = """
        SELECT
            Id,
            Ci,
            NombreCompleto,
            Especialidad,
            Celular
        FROM Mecanicos
        ORDER BY
            NombreCompleto COLLATE NOCASE ASC,
            Ci COLLATE NOCASE ASC;
        """;

    private const string BuscarMecanicosSql = """
        SELECT
            Id,
            Ci,
            NombreCompleto,
            Especialidad,
            Celular
        FROM Mecanicos
        WHERE
            Ci COLLATE NOCASE LIKE @PatronBusqueda ESCAPE '\'
            OR NombreCompleto COLLATE NOCASE LIKE @PatronBusqueda ESCAPE '\'
            OR Especialidad COLLATE NOCASE LIKE @PatronBusqueda ESCAPE '\'
            OR Celular COLLATE NOCASE LIKE @PatronBusqueda ESCAPE '\'
        ORDER BY
            NombreCompleto COLLATE NOCASE ASC,
            Ci COLLATE NOCASE ASC;
        """;

    private const string ActualizarMecanicoSql = """
        UPDATE Mecanicos
        SET
            Ci = @Ci,
            NombreCompleto = @NombreCompleto,
            Especialidad = @Especialidad,
            Celular = @Celular
        WHERE Id = @Id;
        """;

    private const string EliminarMecanicoSql = """
        DELETE FROM Mecanicos
        WHERE Id = @Id;
        """;

    private const string ExisteCiSql = """
        SELECT EXISTS
        (
            SELECT 1
            FROM Mecanicos
            WHERE Ci = @Ci
              AND (@IdExcluido IS NULL OR Id <> @IdExcluido)
        );
        """;

    private readonly DatabaseConnection _databaseConnection;

    public MecanicoRepository(DatabaseConnection databaseConnection)
    {
        _databaseConnection = databaseConnection;
    }

    public async Task<int> CrearAsync(Mecanico mecanico)
    {
        await using var connection = _databaseConnection.CreateConnection();
        await connection.OpenAsync();

        await using var command = new SqliteCommand(CrearMecanicoSql, connection);
        AgregarParametrosMecanico(command, mecanico);

        var resultado = await command.ExecuteScalarAsync();
        if (resultado is not long idGenerado)
        {
            throw new InvalidOperationException(
                "SQLite no devolvió el identificador del mecánico creado.");
        }

        return checked((int)idGenerado);
    }

    public async Task<IReadOnlyList<Mecanico>> ObtenerAsync(
        string? terminoBusqueda = null)
    {
        var tieneTerminoBusqueda = !string.IsNullOrWhiteSpace(terminoBusqueda);
        var consultaSql = tieneTerminoBusqueda
            ? BuscarMecanicosSql
            : ObtenerMecanicosSql;

        await using var connection = _databaseConnection.CreateConnection();
        await connection.OpenAsync();

        await using var command = new SqliteCommand(consultaSql, connection);
        if (tieneTerminoBusqueda)
        {
            command.Parameters.Add("@PatronBusqueda", SqliteType.Text).Value =
                CrearPatronBusqueda(terminoBusqueda!);
        }

        var mecanicos = new List<Mecanico>();
        await using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            mecanicos.Add(MapearMecanico(reader));
        }

        return mecanicos;
    }

    public async Task<bool> ActualizarAsync(Mecanico mecanico)
    {
        await using var connection = _databaseConnection.CreateConnection();
        await connection.OpenAsync();

        await using var command = new SqliteCommand(ActualizarMecanicoSql, connection);
        AgregarParametrosMecanico(command, mecanico);
        command.Parameters.Add("@Id", SqliteType.Integer).Value = mecanico.Id;

        var filasAfectadas = await command.ExecuteNonQueryAsync();
        return filasAfectadas == 1;
    }

    public async Task<bool> EliminarAsync(int id)
    {
        await using var connection = _databaseConnection.CreateConnection();
        await connection.OpenAsync();

        await using var command = new SqliteCommand(EliminarMecanicoSql, connection);
        command.Parameters.Add("@Id", SqliteType.Integer).Value = id;

        var filasAfectadas = await command.ExecuteNonQueryAsync();
        return filasAfectadas == 1;
    }

    public async Task<bool> ExisteCiAsync(
        string ci,
        int? idExcluido = null)
    {
        await using var connection = _databaseConnection.CreateConnection();
        await connection.OpenAsync();

        await using var command = new SqliteCommand(ExisteCiSql, connection);
        command.Parameters.Add("@Ci", SqliteType.Text).Value = ci;

        var idExcluidoParameter =
            command.Parameters.Add("@IdExcluido", SqliteType.Integer);
        idExcluidoParameter.Value = idExcluido.HasValue
            ? idExcluido.Value
            : DBNull.Value;

        var resultado = await command.ExecuteScalarAsync();
        if (resultado is not long existeCi)
        {
            throw new InvalidOperationException(
                "SQLite no devolvió el resultado de la verificación del CI.");
        }

        return existeCi == 1;
    }

    private static void AgregarParametrosMecanico(
        SqliteCommand command,
        Mecanico mecanico)
    {
        command.Parameters.Add("@Ci", SqliteType.Text).Value = mecanico.Ci;
        command.Parameters.Add("@NombreCompleto", SqliteType.Text).Value =
            mecanico.NombreCompleto;
        command.Parameters.Add("@Especialidad", SqliteType.Text).Value =
            mecanico.Especialidad;
        command.Parameters.Add("@Celular", SqliteType.Text).Value = mecanico.Celular;
    }

    private static Mecanico MapearMecanico(SqliteDataReader reader)
    {
        return new Mecanico
        {
            Id = reader.GetInt32(reader.GetOrdinal(nameof(Mecanico.Id))),
            Ci = reader.GetString(reader.GetOrdinal(nameof(Mecanico.Ci))),
            NombreCompleto = reader.GetString(
                reader.GetOrdinal(nameof(Mecanico.NombreCompleto))),
            Especialidad = reader.GetString(
                reader.GetOrdinal(nameof(Mecanico.Especialidad))),
            Celular = reader.GetString(reader.GetOrdinal(nameof(Mecanico.Celular)))
        };
    }

    private static string CrearPatronBusqueda(string terminoBusqueda)
    {
        var terminoEscapado = terminoBusqueda
            .Trim()
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal);

        return $"%{terminoEscapado}%";
    }
}
