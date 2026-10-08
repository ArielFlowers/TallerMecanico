using System.Data.Common;
using TallerMecanico.Data.Factories;

namespace TallerMecanico.Data;

public class DatabaseInitializer
{
    private const string NombreTriggerHistorialCosto =
        "TRG_Servicios_HistorialCosto";

    private readonly DatabaseConnectionFactory _connectionFactory;

    public DatabaseInitializer(
        DatabaseConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public void Initialize()
    {
        using DbConnection connection =
            _connectionFactory.CreateConnection();

        connection.Open();

        CreateMecanicosTable(connection);
        EnsureMecanicosSchema(connection);

        CreateServiciosTable(connection);
        CreateHistorialCostoServiciosTable(connection);
        EnsureHistorialCostoServicioTrigger(connection);

        CreateClientesTable(connection);

        CreateVehiculosTable(connection);
        EnsureVehiculosClienteSchema(connection);

        CreateProductosTable(connection);
    }

    // =====================================================
    // MECÁNICOS
    // =====================================================

    private static void CreateMecanicosTable(
        DbConnection connection)
    {
        const string query = """
            CREATE TABLE IF NOT EXISTS Mecanicos (
                Id INT NOT NULL AUTO_INCREMENT,
                Ci VARCHAR(8) NOT NULL,
                ComplementoCi VARCHAR(2) NOT NULL DEFAULT '',
                Nombres VARCHAR(100) NOT NULL,
                PrimerApellido VARCHAR(100) NOT NULL,
                SegundoApellido VARCHAR(100) NOT NULL,
                Genero VARCHAR(20) NOT NULL,
                Especialidad VARCHAR(60) NOT NULL,
                Celular VARCHAR(20) NOT NULL,

                CONSTRAINT PK_Mecanicos
                    PRIMARY KEY (Id),

                CONSTRAINT UQ_Mecanicos_Ci_ComplementoCi
                    UNIQUE (Ci, ComplementoCi),

                CONSTRAINT CK_Mecanicos_ComplementoCi
                    CHECK (
                        CHAR_LENGTH(ComplementoCi) = 0
                        OR (
                            CHAR_LENGTH(ComplementoCi) = 2
                            AND REGEXP_LIKE(
                                ComplementoCi,
                                '^[0-9][A-Z]$',
                                'c'
                            )
                        )
                    ),

                CONSTRAINT CK_Mecanicos_Genero
                    CHECK (
                        Genero IN (
                            'Masculino',
                            'Femenino'
                        )
                    ),

                CONSTRAINT CK_Mecanicos_Especialidad
                    CHECK (
                        Especialidad IN (
                            'Mecánica Automotriz General',
                            'Motores',
                            'Electricidad Automotriz',
                            'Carrocería Automotriz',
                            'Climatización Automotriz',
                            'Sin Especialidad'
                        )
                    )
            ) DEFAULT CHARSET = utf8mb4;
            """;

        ExecuteCommand(connection, query);
    }

    private static void EnsureMecanicosSchema(
        DbConnection connection)
    {
        EnsureMecanicosApellidoColumns(connection);
        EnsureComplementoCiColumn(connection);
        ValidateMecanicosMigrationValues(connection);
        EnsureNoDuplicateMecanicosCi(connection);
        EnsureMecanicosUniqueIndex(connection);
        MigrateLegacyCiValues(connection);
        EnsureComplementoCiCheck(connection);
    }

    private static void EnsureMecanicosApellidoColumns(
        DbConnection connection)
    {
        var columnas =
            GetMecanicosApellidoColumns(connection);

        var tieneApellidosAntiguos =
            columnas.Contains("Apellidos");

        var tienePrimerApellido =
            columnas.Contains("PrimerApellido");

        var tieneSegundoApellido =
            columnas.Contains("SegundoApellido");

        if (!tieneApellidosAntiguos &&
            tienePrimerApellido &&
            tieneSegundoApellido)
        {
            return;
        }

        EnsureMecanicosEmptyForApellidoMigration(connection);

        var cambios = new List<string>();

        if (!tienePrimerApellido)
        {
            cambios.Add(
                "ADD COLUMN PrimerApellido VARCHAR(100) NOT NULL AFTER Nombres");
        }

        if (!tieneSegundoApellido)
        {
            cambios.Add(
                "ADD COLUMN SegundoApellido VARCHAR(100) NOT NULL AFTER PrimerApellido");
        }

        if (tieneApellidosAntiguos)
        {
            cambios.Add(
                "DROP COLUMN Apellidos");
        }

        ExecuteCommand(
            connection,
            $"ALTER TABLE Mecanicos {string.Join(", ", cambios)};");
    }

    private static HashSet<string> GetMecanicosApellidoColumns(
        DbConnection connection)
    {
        const string query = """
            SELECT COLUMN_NAME
            FROM INFORMATION_SCHEMA.COLUMNS
            WHERE TABLE_SCHEMA = DATABASE()
              AND TABLE_NAME = 'Mecanicos'
              AND COLUMN_NAME IN (
                    'Apellidos',
                    'PrimerApellido',
                    'SegundoApellido'
              );
            """;

        using DbCommand command =
            connection.CreateCommand();

        command.CommandText = query;

        using DbDataReader reader =
            command.ExecuteReader();

        var columnas =
            new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);

        while (reader.Read())
        {
            columnas.Add(
                reader.GetString(0));
        }

        return columnas;
    }

    private static void EnsureMecanicosEmptyForApellidoMigration(
        DbConnection connection)
    {
        const string query = """
            SELECT Id
            FROM Mecanicos
            ORDER BY Id
            LIMIT 1;
            """;

        using DbCommand command =
            connection.CreateCommand();

        command.CommandText = query;

        var idMecanico =
            command.ExecuteScalar();

        if (idMecanico is not null)
        {
            throw new InvalidOperationException(
                "No se puede migrar el esquema de apellidos de Mecanicos: existen registros " +
                $"pendientes de migración manual (primer Id: {idMecanico}). " +
                "Complete PrimerApellido y SegundoApellido como VARCHAR(100) NOT NULL para todos " +
                "los registros y retire la columna antigua Apellidos únicamente después de verificar " +
                "los datos y conservar una copia de respaldo. No se han dividido, " +
                "inventado, eliminado ni modificado apellidos automáticamente.");
        }
    }

    private static void EnsureComplementoCiColumn(
        DbConnection connection)
    {
        const string query = """
            SELECT COUNT(*)
            FROM INFORMATION_SCHEMA.COLUMNS
            WHERE TABLE_SCHEMA = DATABASE()
              AND TABLE_NAME = 'Mecanicos'
              AND COLUMN_NAME = 'ComplementoCi';
            """;

        using DbCommand command =
            connection.CreateCommand();

        command.CommandText = query;

        if (Convert.ToInt32(
                command.ExecuteScalar()) > 0)
        {
            return;
        }

        ExecuteCommand(
            connection,
            """
            ALTER TABLE Mecanicos
            ADD COLUMN ComplementoCi
                VARCHAR(2)
                NOT NULL
                DEFAULT ''
                AFTER Ci;
            """);
    }

    private static void ValidateMecanicosMigrationValues(
        DbConnection connection)
    {
        const string query = """
            SELECT Id,
                   Ci,
                   ComplementoCi
            FROM Mecanicos
            WHERE ComplementoCi IS NULL
               OR (
                    CHAR_LENGTH(ComplementoCi) > 0
                    AND NOT (
                        CHAR_LENGTH(ComplementoCi) = 2
                        AND REGEXP_LIKE(
                            ComplementoCi,
                            '^[0-9][A-Z]$',
                            'c'
                        )
                    )
               )
               OR (
                    LOCATE('-', Ci) > 0
                    AND CHAR_LENGTH(ComplementoCi) = 0
                    AND NOT (
                        CHAR_LENGTH(
                            SUBSTRING(
                                Ci,
                                LOCATE('-', Ci) + 1
                            )
                        ) = 2
                        AND REGEXP_LIKE(
                            Ci,
                            '^[0-9]{5,8}-[0-9][A-Za-z]$',
                            'c'
                        )
                    )
               )
            ORDER BY Id
            LIMIT 1;
            """;

        using DbCommand command =
            connection.CreateCommand();

        command.CommandText = query;

        using DbDataReader reader =
            command.ExecuteReader();

        if (reader.Read())
        {
            var idMecanico =
                reader.GetInt32(0);

            var ci =
                reader.GetString(1);

            var complementoCi =
                reader.IsDBNull(2)
                    ? "NULL"
                    : $"'{reader.GetString(2)}'";

            throw new InvalidOperationException(
                $"No se puede actualizar el esquema de Mecanicos: el registro Id {idMecanico} " +
                $"(Ci = '{ci}', ComplementoCi = {complementoCi}) tiene un formato incompatible. " +
                "El complemento debe estar vacío o tener un número de 0 a 9 seguido de una letra de A a Z. " +
                "Corrija ese registro manualmente; no se han modificado los CI existentes.");
        }
    }

    private static void EnsureNoDuplicateMecanicosCi(
        DbConnection connection)
    {
        const string query = """
            SELECT COUNT(*)
            FROM (
                SELECT
                    CASE
                        WHEN LOCATE('-', Ci) > 0
                             AND CHAR_LENGTH(ComplementoCi) = 0
                        THEN SUBSTRING_INDEX(Ci, '-', 1)
                        ELSE Ci
                    END AS CiMigrado,

                    CASE
                        WHEN LOCATE('-', Ci) > 0
                             AND CHAR_LENGTH(ComplementoCi) = 0
                        THEN UPPER(
                            SUBSTRING(
                                Ci,
                                LOCATE('-', Ci) + 1
                            )
                        )
                        ELSE ComplementoCi
                    END AS ComplementoMigrado

                FROM Mecanicos

                GROUP BY
                    CiMigrado,
                    ComplementoMigrado

                HAVING COUNT(*) > 1
            ) AS CombinacionesDuplicadas;
            """;

        using DbCommand command =
            connection.CreateCommand();

        command.CommandText = query;

        if (Convert.ToInt32(
                command.ExecuteScalar()) > 0)
        {
            throw new InvalidOperationException(
                "No se puede migrar Mecanicos: existen combinaciones duplicadas de" +
                "(Ci, ComplementoCi), considerando también los CI antiguos separados. " +
                "Resuelva los duplicados manualmente; no se han eliminado ni modificado registros.");
        }
    }

    private static void EnsureMecanicosUniqueIndex(
        DbConnection connection)
    {
        const string nombreIndiceCompuesto =
            "UQ_Mecanicos_Ci_ComplementoCi";

        var indicesCi =
            GetMecanicosUniqueCiIndexes(connection);

        var tieneIndiceCompuesto =
            MecanicosCompositeUniqueIndexExists(connection);

        var cambios =
            indicesCi
                .Select(
                    nombreIndice =>
                        $"DROP INDEX `{nombreIndice.Replace(
                            "`",
                            "``",
                            StringComparison.Ordinal)}`")
                .ToList();

        if (!tieneIndiceCompuesto)
        {
            if (MecanicosIndexNameExists(
                    connection,
                    nombreIndiceCompuesto)
                &&
                !indicesCi.Contains(
                    nombreIndiceCompuesto))
            {
                throw new InvalidOperationException(
                    $"El índice {nombreIndiceCompuesto} de Mecanicos ya existe conotra definición. " +
                    "Revise su definición manualmente antes de migrar.");
            }

            cambios.Add(
                $"ADD CONSTRAINT {nombreIndiceCompuesto} UNIQUE (Ci, ComplementoCi)");
        }

        if (cambios.Count > 0)
        {
            ExecuteCommand(
                connection,
                $"ALTER TABLE Mecanicos {string.Join(", ", cambios)};");
        }
    }

    private static List<string> GetMecanicosUniqueCiIndexes(
        DbConnection connection)
    {
        const string query = """
            SELECT INDEX_NAME
            FROM INFORMATION_SCHEMA.STATISTICS
            WHERE TABLE_SCHEMA = DATABASE()
              AND TABLE_NAME = 'Mecanicos'
              AND NON_UNIQUE = 0
              AND INDEX_NAME <> 'PRIMARY'
            GROUP BY INDEX_NAME
            HAVING COUNT(*) = 1
               AND MAX(COLUMN_NAME) = 'Ci';
            """;

        using DbCommand command =
            connection.CreateCommand();

        command.CommandText = query;

        using DbDataReader reader =
            command.ExecuteReader();

        var nombresIndices =
            new List<string>();

        while (reader.Read())
        {
            nombresIndices.Add(
                reader.GetString(0));
        }

        return nombresIndices;
    }

    private static bool MecanicosCompositeUniqueIndexExists(
        DbConnection connection)
    {
        const string query = """
            SELECT COUNT(*)
            FROM (
                SELECT INDEX_NAME
                FROM INFORMATION_SCHEMA.STATISTICS
                WHERE TABLE_SCHEMA = DATABASE()
                  AND TABLE_NAME = 'Mecanicos'
                  AND NON_UNIQUE = 0
                GROUP BY INDEX_NAME
                HAVING COUNT(*) = 2
                   AND MAX(
                        CASE
                            WHEN SEQ_IN_INDEX = 1
                            THEN COLUMN_NAME
                        END
                   ) = 'Ci'
                   AND MAX(
                        CASE
                            WHEN SEQ_IN_INDEX = 2
                            THEN COLUMN_NAME
                        END
                   ) = 'ComplementoCi'
                   AND COUNT(SUB_PART) = 0
            ) AS IndicesCompuestos;
            """;

        using DbCommand command =
            connection.CreateCommand();

        command.CommandText = query;

        return Convert.ToInt32(
            command.ExecuteScalar()) > 0;
    }

    private static bool MecanicosIndexNameExists(
        DbConnection connection,
        string nombreIndice)
    {
        const string query = """
            SELECT COUNT(*)
            FROM INFORMATION_SCHEMA.STATISTICS
            WHERE TABLE_SCHEMA = DATABASE()
              AND TABLE_NAME = 'Mecanicos'
              AND INDEX_NAME = @NombreIndice;
            """;

        using DbCommand command =
            connection.CreateCommand();

        command.CommandText = query;

        AddParameter(
            command,
            "@NombreIndice",
            nombreIndice);

        return Convert.ToInt32(
            command.ExecuteScalar()) > 0;
    }

    private static void MigrateLegacyCiValues(
        DbConnection connection)
    {
        const string query = """
            UPDATE Mecanicos
            SET ComplementoCi =
                    UPPER(
                        SUBSTRING(
                            Ci,
                            LOCATE('-', Ci) + 1
                        )
                    ),
                Ci =
                    SUBSTRING_INDEX(
                        Ci,
                        '-',
                        1
                    )
            WHERE LOCATE('-', Ci) > 0
              AND CHAR_LENGTH(ComplementoCi) = 0;
            """;

        ExecuteCommand(connection, query);
    }

    private static void EnsureComplementoCiCheck(
        DbConnection connection)
    {
        const string patronComplementoCi =
            "^[0-9][A-Z]$";

        const string query = """
            SELECT Restriccion.ENFORCED,
                   Definicion.CHECK_CLAUSE
            FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS AS Restriccion
            JOIN INFORMATION_SCHEMA.CHECK_CONSTRAINTS AS Definicion
              ON Definicion.CONSTRAINT_SCHEMA =
                    Restriccion.CONSTRAINT_SCHEMA
             AND Definicion.CONSTRAINT_NAME =
                    Restriccion.CONSTRAINT_NAME
            WHERE Restriccion.CONSTRAINT_SCHEMA =
                    DATABASE()
              AND Restriccion.TABLE_NAME =
                    'Mecanicos'
              AND Restriccion.CONSTRAINT_NAME =
                    'CK_Mecanicos_ComplementoCi'
              AND Restriccion.CONSTRAINT_TYPE =
                    'CHECK';
            """;

        using DbCommand command =
            connection.CreateCommand();

        command.CommandText = query;

        string? estadoRestriccion = null;
        string? definicionRestriccion = null;

        using (DbDataReader reader =
               command.ExecuteReader())
        {
            if (reader.Read())
            {
                estadoRestriccion =
                    reader.GetString(0);

                definicionRestriccion =
                    reader.GetString(1);
            }
        }

        if (definicionRestriccion is null
            ||
            !definicionRestriccion.Contains(
                patronComplementoCi,
                StringComparison.Ordinal))
        {
            var eliminarRestriccionAnterior =
                definicionRestriccion is null
                    ? string.Empty
                    : "DROP CHECK CK_Mecanicos_ComplementoCi, ";

            ExecuteCommand(
                connection,
                $"""
                ALTER TABLE Mecanicos
                {eliminarRestriccionAnterior}
                ADD CONSTRAINT CK_Mecanicos_ComplementoCi
                CHECK (
                    CHAR_LENGTH(ComplementoCi) = 0
                    OR (
                        CHAR_LENGTH(ComplementoCi) = 2
                        AND REGEXP_LIKE(
                            ComplementoCi,
                            '{patronComplementoCi}',
                            'c'
                        )
                    )
                );
                """);
        }
        else if (string.Equals(
                     estadoRestriccion,
                     "NO",
                     StringComparison.OrdinalIgnoreCase))
        {
            ExecuteCommand(
                connection,
                """
                ALTER TABLE Mecanicos
                ALTER CHECK
                    CK_Mecanicos_ComplementoCi
                ENFORCED;
                """);
        }
    }

    // =====================================================
    // SERVICIOS
    // =====================================================

    private static void CreateServiciosTable(
        DbConnection connection)
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

    // =====================================================
    // HISTORIAL
    // =====================================================

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
        if (TriggerExiste(
                connection,
                NombreTriggerHistorialCosto))
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

        using DbCommand command =
            connection.CreateCommand();

        command.CommandText = query;

        AddParameter(
            command,
            "@NombreTrigger",
            nombreTrigger);

        return Convert.ToInt32(
            command.ExecuteScalar()) > 0;
    }

    // =====================================================
    // CLIENTES
    // =====================================================

    private static void CreateClientesTable(
        DbConnection connection)
    {
        const string query = """
            CREATE TABLE IF NOT EXISTS Clientes (
                Id INT NOT NULL AUTO_INCREMENT,
                Ci VARCHAR(8) NOT NULL,
                ComplementoCi VARCHAR(2) NOT NULL DEFAULT '',
                Nombres VARCHAR(100) NOT NULL,
                PrimerApellido VARCHAR(100) NOT NULL,
                SegundoApellido VARCHAR(100) NOT NULL,
                Celular VARCHAR(8) NOT NULL,
                CreadoPor VARCHAR(50) NOT NULL DEFAULT 'sistema',
                FechaCreacion DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,

                CONSTRAINT PK_Clientes
                    PRIMARY KEY (Id),

                CONSTRAINT UQ_Clientes_Ci_ComplementoCi
                    UNIQUE (Ci, ComplementoCi),

                CONSTRAINT CK_Clientes_Ci
                    CHECK (
                        CHAR_LENGTH(Ci) BETWEEN 5 AND 8
                        AND Ci REGEXP '^[0-9]{5,8}$'
                    ),

                CONSTRAINT CK_Clientes_ComplementoCi
                    CHECK (
                        CHAR_LENGTH(ComplementoCi) = 0
                        OR (
                            CHAR_LENGTH(ComplementoCi) = 2
                            AND REGEXP_LIKE(
                                ComplementoCi,
                                '^[0-9][A-Z]$',
                                'c'
                            )
                        )
                    ),

                CONSTRAINT CK_Clientes_Celular
                    CHECK (
                        CHAR_LENGTH(Celular) = 8
                        AND REGEXP_LIKE(
                            Celular,
                            '^[67][0-9]{7}$',
                            'c'
                        )
                    )
            ) DEFAULT CHARSET = utf8mb4;
            """;

        ExecuteCommand(connection, query);
    }

    // =====================================================
    // VEHÍCULOS
    // =====================================================

    private static void CreateVehiculosTable(
        DbConnection connection)
    {
        const string query = """
            CREATE TABLE IF NOT EXISTS Vehiculos (
                Id INT NOT NULL AUTO_INCREMENT,
                Placa VARCHAR(10) NOT NULL UNIQUE,
                Marca VARCHAR(60) NOT NULL DEFAULT '',
                Modelo VARCHAR(100) NOT NULL,
                Kilometraje INT NOT NULL,
                Observaciones VARCHAR(300) NOT NULL DEFAULT '',
                ClienteId INT NULL,

                CONSTRAINT PK_Vehiculos
                    PRIMARY KEY (Id),

                CONSTRAINT CK_Vehiculos_Kilometraje
                    CHECK (Kilometraje >= 0),

                INDEX IX_Vehiculos_ClienteId (ClienteId),

                CONSTRAINT FK_Vehiculos_Clientes
                    FOREIGN KEY (ClienteId)
                    REFERENCES Clientes(Id)
            ) DEFAULT CHARSET = utf8mb4;
            """;

        ExecuteCommand(connection, query);
    }

    private static void EnsureVehiculosClienteSchema(
        DbConnection connection)
    {
        EnsureVehiculosClienteIdColumn(connection);
        EnsureVehiculosClienteIdIndex(connection);
        EnsureVehiculosClienteForeignKey(connection);
    }

    private static void EnsureVehiculosClienteIdColumn(
        DbConnection connection)
    {
        const string query = """
            SELECT COUNT(*)
            FROM INFORMATION_SCHEMA.COLUMNS
            WHERE TABLE_SCHEMA = DATABASE()
              AND TABLE_NAME = 'Vehiculos'
              AND COLUMN_NAME = 'ClienteId';
            """;

        using DbCommand command =
            connection.CreateCommand();

        command.CommandText = query;

        bool existe =
            Convert.ToInt32(
                command.ExecuteScalar()) > 0;

        if (existe)
        {
            return;
        }

        ExecuteCommand(
            connection,
            """
            ALTER TABLE Vehiculos
            ADD COLUMN ClienteId INT NULL
            AFTER Observaciones;
            """);
    }

    private static void EnsureVehiculosClienteIdIndex(
        DbConnection connection)
    {
        const string query = """
            SELECT COUNT(*)
            FROM INFORMATION_SCHEMA.STATISTICS
            WHERE TABLE_SCHEMA = DATABASE()
              AND TABLE_NAME = 'Vehiculos'
              AND COLUMN_NAME = 'ClienteId';
            """;

        using DbCommand command =
            connection.CreateCommand();

        command.CommandText = query;

        bool existe =
            Convert.ToInt32(
                command.ExecuteScalar()) > 0;

        if (existe)
        {
            return;
        }

        ExecuteCommand(
            connection,
            """
            CREATE INDEX IX_Vehiculos_ClienteId
            ON Vehiculos (ClienteId);
            """);
    }

    private static void EnsureVehiculosClienteForeignKey(
        DbConnection connection)
    {
        const string query = """
            SELECT COUNT(*)
            FROM INFORMATION_SCHEMA.KEY_COLUMN_USAGE
            WHERE TABLE_SCHEMA = DATABASE()
              AND TABLE_NAME = 'Vehiculos'
              AND COLUMN_NAME = 'ClienteId'
              AND REFERENCED_TABLE_NAME = 'Clientes'
              AND REFERENCED_COLUMN_NAME = 'Id';
            """;

        using DbCommand command =
            connection.CreateCommand();

        command.CommandText = query;

        bool existe =
            Convert.ToInt32(
                command.ExecuteScalar()) > 0;

        if (existe)
        {
            return;
        }

        ExecuteCommand(
            connection,
            """
            ALTER TABLE Vehiculos
            ADD CONSTRAINT FK_Vehiculos_Clientes
                FOREIGN KEY (ClienteId)
                REFERENCES Clientes(Id);
            """);
    }

    // =====================================================
    // PRODUCTOS
    // =====================================================

    private static void CreateProductosTable(
        DbConnection connection)
    {
        const string query = """
            CREATE TABLE IF NOT EXISTS Productos (
                Id INT NOT NULL AUTO_INCREMENT,
                Codigo VARCHAR(50) NOT NULL,
                Nombre VARCHAR(100) NOT NULL,
                Precio DECIMAL(10,2) NOT NULL,
                Stock INT NOT NULL,
                StockMinimo INT NOT NULL,
                CreadoPor VARCHAR(50) NOT NULL DEFAULT 'sistema',
                FechaCreacion DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,

                CONSTRAINT PK_Productos
                    PRIMARY KEY (Id),

                CONSTRAINT UQ_Productos_Codigo
                    UNIQUE (Codigo),

                CONSTRAINT CK_Productos_Precio
                    CHECK (Precio > 0),

                CONSTRAINT CK_Productos_Stock
                    CHECK (Stock >= 0),

                CONSTRAINT CK_Productos_StockMinimo
                    CHECK (StockMinimo >= 0)
            ) DEFAULT CHARSET = utf8mb4;
            """;

        ExecuteCommand(
            connection,
            query);
    }

    // =====================================================
    // HELPERS
    // =====================================================

    private static void ExecuteCommand(
        DbConnection connection,
        string query)
    {
        using DbCommand command =
            connection.CreateCommand();

        command.CommandText = query;

        command.ExecuteNonQuery();
    }

    private static void AddParameter(
        DbCommand command,
        string nombre,
        object valor)
    {
        DbParameter parameter =
            command.CreateParameter();

        parameter.ParameterName =
            nombre;

        parameter.Value =
            valor;

        command.Parameters.Add(
            parameter);
    }
}
