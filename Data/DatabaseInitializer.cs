using Microsoft.Data.Sqlite;

namespace TallerMecanico.Data;

public class DatabaseInitializer
{
    private const string CreateMecanicosTableSql = """
        CREATE TABLE IF NOT EXISTS Mecanicos
        (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            Ci TEXT NOT NULL UNIQUE,
            NombreCompleto TEXT NOT NULL,
            Especialidad TEXT NOT NULL,
            Celular TEXT NOT NULL
        );
        """;

    private readonly DatabaseConnection _databaseConnection;

    public DatabaseInitializer(DatabaseConnection databaseConnection)
    {
        _databaseConnection = databaseConnection;
    }

    public async Task InitializeAsync()
    {
        await using var connection = _databaseConnection.CreateConnection();
        await connection.OpenAsync();

        await using var command = new SqliteCommand(CreateMecanicosTableSql, connection);
        await command.ExecuteNonQueryAsync();
    }
}
