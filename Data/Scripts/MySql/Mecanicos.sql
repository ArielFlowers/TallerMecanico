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
