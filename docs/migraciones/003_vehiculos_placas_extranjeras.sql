-- Compatible con MySQL 8. Se puede volver a ejecutar sin perder registros.
SET @vehiculos_ddl = IF(
    (SELECT CHARACTER_MAXIMUM_LENGTH FROM INFORMATION_SCHEMA.COLUMNS
     WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'Vehiculos' AND COLUMN_NAME = 'Placa') < 15,
    'ALTER TABLE Vehiculos MODIFY COLUMN Placa VARCHAR(15) NOT NULL',
    'SELECT 1');
PREPARE vehiculos_migracion FROM @vehiculos_ddl;
EXECUTE vehiculos_migracion;
DEALLOCATE PREPARE vehiculos_migracion;

SET @vehiculos_ddl = IF(
    (SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS
     WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'Vehiculos' AND COLUMN_NAME = 'EsPlacaExtranjera') = 0,
    'ALTER TABLE Vehiculos ADD COLUMN EsPlacaExtranjera BOOLEAN NOT NULL DEFAULT FALSE AFTER Placa',
    'SELECT 1');
PREPARE vehiculos_migracion FROM @vehiculos_ddl;
EXECUTE vehiculos_migracion;
DEALLOCATE PREPARE vehiculos_migracion;
