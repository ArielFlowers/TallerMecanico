using Microsoft.Data.Sqlite;

namespace TallerMecanico.Data;

public class DatabaseInitializer
{
    private readonly DatabaseConnection _databaseConnection;

    public DatabaseInitializer(DatabaseConnection databaseConnection)
    {
        _databaseConnection = databaseConnection;
    }

    public void Initialize()
    {
        using SqliteConnection connection = _databaseConnection.CreateConnection();
        connection.Open();

        const string createServiciosTableQuery = """
            CREATE TABLE IF NOT EXISTS Servicios (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Nombre TEXT NOT NULL,
                Descripcion TEXT NOT NULL,
                Costo REAL NOT NULL CHECK (Costo > 0),
                TiempoEstimadoHoras REAL NOT NULL CHECK (TiempoEstimadoHoras > 0)
            );
            """;

        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = createServiciosTableQuery;
        command.ExecuteNonQuery();
    }
}
