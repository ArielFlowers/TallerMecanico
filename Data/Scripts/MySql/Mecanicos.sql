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
                AND REGEXP_LIKE(ComplementoCi, '^[0-9][A-Z]$', 'c')
            )
        ),

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
