-- RF-07: la duración de los tokens se configura en la base de datos.
-- Se puede ejecutar más de una vez y no elimina datos.
-- DatabaseInitializer también aplica este cambio al iniciar la aplicación.

CREATE TABLE IF NOT EXISTS ConfiguracionTokens
(
    Clave VARCHAR(50) NOT NULL,
    DuracionMinutos INT NOT NULL,
    Descripcion VARCHAR(200) NULL,

    CONSTRAINT PK_ConfiguracionTokens
        PRIMARY KEY (Clave),

    CONSTRAINT CK_ConfiguracionTokens_Duracion
        CHECK (DuracionMinutos BETWEEN 1 AND 10080)
) ENGINE=InnoDB
  DEFAULT CHARSET=utf8mb4;

-- INSERT IGNORE conserva los valores que ya se hayan cambiado.
INSERT IGNORE INTO ConfiguracionTokens
    (Clave, DuracionMinutos, Descripcion)
VALUES
    ('VerificacionEmail', 60, 'Enlace para confirmar el correo del registro'),
    ('InvitacionUsuario', 60, 'Enlace de invitación que envía el administrador'),
    ('SesionUsuario', 30, 'Sesión iniciada sin actividad');

-- Hasta cuándo dura la sesión abierta de cada usuario (NULL = sin sesión).
SET @ddl = IF(
    (SELECT COUNT(*) FROM information_schema.columns
     WHERE table_schema = DATABASE() AND table_name = 'Usuarios' AND column_name = 'SesionVenceEn') = 0,
    'ALTER TABLE Usuarios ADD COLUMN SesionVenceEn DATETIME(6) NULL',
    'SELECT 1'
);
PREPARE migracion FROM @ddl;
EXECUTE migracion;
DEALLOCATE PREPARE migracion;

-- Ejemplo: cambiar la duración de la sesión a 15 minutos.
-- UPDATE ConfiguracionTokens SET DuracionMinutos = 15 WHERE Clave = 'SesionUsuario';
