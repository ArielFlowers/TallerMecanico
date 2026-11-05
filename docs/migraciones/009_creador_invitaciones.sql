-- Guarda al administrador que invitó a cada usuario.
-- Se puede ejecutar más de una vez y no elimina datos.
-- DatabaseInitializer también aplica este cambio al iniciar la aplicación.

SET @ddl = IF(
    (SELECT COUNT(*) FROM information_schema.columns
     WHERE table_schema = DATABASE() AND table_name = 'SolicitudesRegistro' AND column_name = 'CreadoPor') = 0,
    'ALTER TABLE SolicitudesRegistro ADD COLUMN CreadoPor VARCHAR(50) NULL',
    'SELECT 1'
);
PREPARE migracion FROM @ddl;
EXECUTE migracion;
DEALLOCATE PREPARE migracion;
