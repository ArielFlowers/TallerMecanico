using System.Data.Common;
using TallerMecanico.Data.Factories;

namespace TallerMecanico.Data;

public class DatabaseInitializer
{
    private const string NombreTriggerHistorialCosto =
        "TRG_Servicios_HistorialCosto";

    private readonly DatabaseConnectionFactory _connectionFactory;

    public DatabaseInitializer(DatabaseConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public void Initialize()
    {
        using DbConnection connection =
            _connectionFactory.CreateConnection();

        connection.Open();

        CreateMecanicosTable(connection);
        CreateServiciosTable(connection);
        CreateHistorialCostoServiciosTable(connection);
        EnsureHistorialCostoServicioTrigger(connection);
        CreateVehiculosTable(connection);
    }

    private static void CreateMecanicosTable(DbConnection connection)
    {
        const string query = """
            CREATE TABLE IF NOT EXISTS Mecanicos (
                Id INT NOT NULL AUTO_INCREMENT,
                Ci VARCHAR(20) NOT NULL UNIQUE,
                Nombres VARCHAR(100) NOT NULL,
                Apellidos VARCHAR(100) NOT NULL,
                Genero VARCHAR(20) NOT NULL,
                Especialidad VARCHAR(60) NOT NULL,
                Celular VARCHAR(20) NOT NULL,

                CONSTRAINT PK_Mecanicos
                    PRIMARY KEY (Id),

                CONSTRAINT CK_Mecanicos_Genero
                    CHECK (Genero IN ('Masculino', 'Femenino')),

                CONSTRAINT CK_Mecanicos_Especialidad
                    CHECK (Especialidad IN (
                        'Mecánica Automotriz General',
                        'Motores',
                        'Electricidad Automotriz',
                        'Carrocería Automotriz',
                        'Climatización Automotriz',
                        'Sin Especialidad'
                    ))
            ) DEFAULT CHARSET = utf8mb4;
            """;

        ExecuteCommand(connection, query);
    }

    private static void CreateServiciosTable(DbConnection connection)
    {
        const string query = """
            CREATE TABLE IF NOT EXISTS Servicios (
                Id INT NOT NULL AUTO_INCREMENT,
                Nombre VARCHAR(100) NOT NULL,
                Descripcion VARCHAR(300) NOT NULL,
                Costo DECIMAL(10,2) NOT NULL,
                TiempoEstimadoHoras DECIMAL(6,2) NOT NULL,

                CONSTRAINT PK_Servicios
                    PRIMARY KEY (Id),

                CONSTRAINT CK_Servicios_Costo
                    CHECK (Costo > 0),

                CONSTRAINT CK_Servicios_TiempoEstimadoHoras
                    CHECK (TiempoEstimadoHoras > 0)
            ) DEFAULT CHARSET = utf8mb4;
            """;

        ExecuteCommand(connection, query);
    }

    private static void CreateHistorialCostoServiciosTable(
        DbConnection connection)
    {
        const string query = """
            CREATE TABLE IF NOT EXISTS HistorialCostoServicios (
                Id INT NOT NULL AUTO_INCREMENT,
                ServicioId INT NOT NULL,
                NombreServicio VARCHAR(100) NOT NULL,
                CostoAnterior DECIMAL(10,2) NOT NULL,
                CostoNuevo DECIMAL(10,2) NOT NULL,
                FechaCambio DATETIME NOT NULL,

                CONSTRAINT PK_HistorialCostoServicios
                    PRIMARY KEY (Id)
            ) DEFAULT CHARSET = utf8mb4;
            """;

        ExecuteCommand(connection, query);
    }

    private static void EnsureHistorialCostoServicioTrigger(
        DbConnection connection)
    {
        if (TriggerExiste(connection, NombreTriggerHistorialCosto))
        {
            return;
        }

        const string query = """
            CREATE TRIGGER TRG_Servicios_HistorialCosto
            AFTER UPDATE ON Servicios
            FOR EACH ROW
            BEGIN
                IF OLD.Costo <> NEW.Costo THEN
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
                        NOW()
                    );
                END IF;
            END;
            """;

        ExecuteCommand(connection, query);
    }

    private static bool TriggerExiste(
        DbConnection connection,
        string nombreTrigger)
    {
        const string query = """
            SELECT COUNT(*)
            FROM INFORMATION_SCHEMA.TRIGGERS
            WHERE TRIGGER_SCHEMA = DATABASE()
              AND TRIGGER_NAME = @NombreTrigger;
            """;

        using DbCommand command = connection.CreateCommand();
        command.CommandText = query;
        AddParameter(command, "@NombreTrigger", nombreTrigger);

        return Convert.ToInt32(command.ExecuteScalar()) > 0;
    }

    private static void CreateVehiculosTable(DbConnection connection)
    {
        const string query = """
            CREATE TABLE IF NOT EXISTS Vehiculos (
                Id INT NOT NULL AUTO_INCREMENT,
                Placa VARCHAR(10) NOT NULL UNIQUE,
                Marca VARCHAR(60) NOT NULL DEFAULT '',
                Modelo VARCHAR(100) NOT NULL,
                Kilometraje INT NOT NULL,
                Observaciones VARCHAR(300) NOT NULL DEFAULT '',

                CONSTRAINT PK_Vehiculos
                    PRIMARY KEY (Id),

                CONSTRAINT CK_Vehiculos_Kilometraje
                    CHECK (Kilometraje >= 0)
            ) DEFAULT CHARSET = utf8mb4;
            """;

        ExecuteCommand(connection, query);
    }

    private static void ExecuteCommand(
        DbConnection connection,
        string query)
    {
        using DbCommand command = connection.CreateCommand();
        command.CommandText = query;
        command.ExecuteNonQuery();
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
}
