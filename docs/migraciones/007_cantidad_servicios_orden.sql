-- Permite configurar servicios cobrados por pieza y conservar cantidades históricas.
-- DatabaseInitializer aplica estos cambios automáticamente al iniciar.

SET @ddl = IF(
    (SELECT COUNT(*) FROM information_schema.columns
     WHERE table_schema = DATABASE() AND table_name = 'Servicios' AND column_name = 'UnidadCantidad') = 0,
    'ALTER TABLE Servicios ADD COLUMN UnidadCantidad VARCHAR(20) NULL',
    'SELECT 1'
);
PREPARE migracion FROM @ddl;
EXECUTE migracion;
DEALLOCATE PREPARE migracion;

SET @ddl = IF(
    (SELECT COUNT(*) FROM information_schema.columns
     WHERE table_schema = DATABASE() AND table_name = 'DetalleServiciosOrdenes' AND column_name = 'Cantidad') = 0,
    'ALTER TABLE DetalleServiciosOrdenes ADD COLUMN Cantidad INT NOT NULL DEFAULT 1',
    'SELECT 1'
);
PREPARE migracion FROM @ddl;
EXECUTE migracion;
DEALLOCATE PREPARE migracion;

SET @ddl = IF(
    (SELECT COUNT(*) FROM information_schema.columns
     WHERE table_schema = DATABASE() AND table_name = 'DetalleServiciosOrdenes' AND column_name = 'UnidadCantidad') = 0,
    'ALTER TABLE DetalleServiciosOrdenes ADD COLUMN UnidadCantidad VARCHAR(20) NULL',
    'SELECT 1'
);
PREPARE migracion FROM @ddl;
EXECUTE migracion;
DEALLOCATE PREPARE migracion;
