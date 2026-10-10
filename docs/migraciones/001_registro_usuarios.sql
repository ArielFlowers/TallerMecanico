-- MIGRACION 001 - REGISTRO DE USUARIOS
-- Ejecutar una sola vez en la base tallermecanico.
-- Conserva las cuentas existentes.

-- Agregar correo a usuarios existentes y futuros.
ALTER TABLE Usuarios
    ADD COLUMN Email VARCHAR(254) NULL,
    ADD COLUMN EmailVerificado BOOLEAN NOT NULL DEFAULT FALSE,
    ADD CONSTRAINT UQ_Usuarios_Email UNIQUE (Email);

-- Solicitudes públicas pendientes de aprobación.
-- Aquí no se asignan roles administrativos.
CREATE TABLE SolicitudesRegistro
(
    Id BIGINT NOT NULL AUTO_INCREMENT,
    Username VARCHAR(50) NOT NULL,
    Email VARCHAR(254) NOT NULL,
    PasswordHash VARCHAR(255) NOT NULL,

    Estado ENUM(
        'PendienteVerificacion',
        'PendienteAprobacion',
        'Aprobada',
        'Rechazada'
    ) NOT NULL DEFAULT 'PendienteVerificacion',

    TokenVerificacionHash CHAR(64) NULL,
    TokenExpiraEn DATETIME(6) NULL,
    EmailVerificadoEn DATETIME(6) NULL,

    FechaSolicitud DATETIME(6) NOT NULL,
    FechaResolucion DATETIME(6) NULL,
    RevisadoPorUsuarioId INT NULL,

    CONSTRAINT PK_SolicitudesRegistro PRIMARY KEY (Id),

    CONSTRAINT UQ_SolicitudesRegistro_Username
        UNIQUE (Username),

    CONSTRAINT UQ_SolicitudesRegistro_Email
        UNIQUE (Email),

    CONSTRAINT UQ_SolicitudesRegistro_Token
        UNIQUE (TokenVerificacionHash),

    INDEX IX_SolicitudesRegistro_Estado (Estado)
) ENGINE=InnoDB
  DEFAULT CHARSET=utf8mb4;