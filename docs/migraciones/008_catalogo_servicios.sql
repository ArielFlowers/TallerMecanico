-- Solo agrega la identidad opcional. No carga ni modifica servicios existentes.
SET @ddl = IF(
    (SELECT COUNT(*) FROM information_schema.columns
     WHERE table_schema = DATABASE() AND table_name = 'Servicios' AND column_name = 'CodigoCatalogo') = 0,
    'ALTER TABLE Servicios ADD COLUMN CodigoCatalogo VARCHAR(40) CHARACTER SET ascii COLLATE ascii_bin NULL',
    'SELECT 1'
);
PREPARE migracion FROM @ddl;
EXECUTE migracion;
DEALLOCATE PREPARE migracion;

SET @ddl = IF(
    (SELECT COUNT(*) FROM (
        SELECT index_name FROM information_schema.statistics
        WHERE table_schema = DATABASE() AND table_name = 'Servicios' AND non_unique = 0
        GROUP BY index_name
        HAVING COUNT(*) = 1 AND MAX(column_name) = 'CodigoCatalogo' AND MAX(sub_part) IS NULL
    ) AS indices_catalogo) = 0,
    'ALTER TABLE Servicios ADD CONSTRAINT UQ_Servicios_CodigoCatalogo UNIQUE (CodigoCatalogo)',
    'SELECT 1'
);
PREPARE migracion FROM @ddl;
EXECUTE migracion;
DEALLOCATE PREPARE migracion;
