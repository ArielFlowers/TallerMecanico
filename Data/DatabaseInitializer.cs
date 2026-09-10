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
        using SqliteConnection connection =
            _databaseConnection.CreateConnection();

        connection.Open();

        CreateMecanicosTable(connection);
        CreateServiciosTable(connection);
        CreateHistorialCostoServiciosTable(connection);
        CreateHistorialCostoServicioTrigger(connection);
        CreateVehiculosTable(connection);
    }

    private static void CreateMecanicosTable(SqliteConnection connection)
    {
        const string query = """
            CREATE TABLE IF NOT EXISTS Mecanicos
            (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Ci TEXT NOT NULL UNIQUE,
                NombreCompleto TEXT NOT NULL,
                Especialidad TEXT NOT NULL,
                Celular TEXT NOT NULL
            );
            """;

        ExecuteCommand(connection, query);
    }

    private static void CreateServiciosTable(SqliteConnection connection)
    {
        const string query = """
            CREATE TABLE IF NOT EXISTS Servicios (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Nombre TEXT NOT NULL,
                Descripcion TEXT NOT NULL,
                Costo REAL NOT NULL CHECK (Costo > 0),
                TiempoEstimadoHoras REAL NOT NULL CHECK (TiempoEstimadoHoras > 0)
            );
            """;

        ExecuteCommand(connection, query);
    }

    private static void CreateHistorialCostoServiciosTable(
        SqliteConnection connection)
    {
        const string query = """
            CREATE TABLE IF NOT EXISTS HistorialCostoServicios (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                ServicioId INTEGER NOT NULL,
                NombreServicio TEXT NOT NULL,
                CostoAnterior REAL NOT NULL,
                CostoNuevo REAL NOT NULL,
                FechaCambio TEXT NOT NULL
            );
            """;

        ExecuteCommand(connection, query);
    }

    private static void CreateHistorialCostoServicioTrigger(
        SqliteConnection connection)
    {
        const string query = """
            CREATE TRIGGER IF NOT EXISTS TRG_Servicios_HistorialCosto
            AFTER UPDATE OF Costo ON Servicios
            WHEN OLD.Costo <> NEW.Costo
            BEGIN
                INSERT INTO HistorialCostoServicios (
                    ServicioId,
                    NombreServicio,
                    CostoAnterior,
                    CostoNuevo,
                    FechaCambio
                )
                VALUES (
                    NEW.Id,
                    NEW.Nombre,
                    OLD.Costo,
                    NEW.Costo,
                    datetime('now', 'localtime')
                );
            END;
            """;

        ExecuteCommand(connection, query);
    }

    private static void CreateVehiculosTable(SqliteConnection connection)
    {
        const string query = """
            CREATE TABLE IF NOT EXISTS Vehiculos (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Placa TEXT NOT NULL UNIQUE,
                Modelo TEXT NOT NULL,
                Kilometraje INTEGER NOT NULL CHECK (Kilometraje >= 0),
                Observaciones TEXT NOT NULL DEFAULT ''
            );
            """;

        ExecuteCommand(connection, query);
    }

    private static void ExecuteCommand(
        SqliteConnection connection,
        string query)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = query;
        command.ExecuteNonQuery();
    }
}