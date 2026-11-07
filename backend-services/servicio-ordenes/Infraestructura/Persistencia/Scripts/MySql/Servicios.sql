CREATE TABLE IF NOT EXISTS Servicios (
    Id INT NOT NULL AUTO_INCREMENT,
    CodigoCatalogo VARCHAR(40) CHARACTER SET ascii COLLATE ascii_bin NULL,
    Nombre VARCHAR(100) NOT NULL,
    Descripcion VARCHAR(300) NOT NULL,
    Costo DECIMAL(10,2) NOT NULL,
    TiempoEstimadoHoras DECIMAL(6,2) NOT NULL,

    CONSTRAINT PK_Servicios
        PRIMARY KEY (Id),

    CONSTRAINT UQ_Servicios_CodigoCatalogo
        UNIQUE (CodigoCatalogo),

    CONSTRAINT CK_Servicios_Costo
        CHECK (Costo > 0),

    CONSTRAINT CK_Servicios_TiempoEstimadoHoras
        CHECK (TiempoEstimadoHoras > 0)
) DEFAULT CHARSET = utf8mb4;
