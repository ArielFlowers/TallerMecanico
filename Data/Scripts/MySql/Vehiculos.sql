CREATE TABLE IF NOT EXISTS Vehiculos (
    Id INT NOT NULL AUTO_INCREMENT,
    Placa VARCHAR(10) NOT NULL UNIQUE,
    Marca VARCHAR(60) NOT NULL DEFAULT '',
    Modelo VARCHAR(100) NOT NULL,
    Kilometraje INT NOT NULL,
    Observaciones VARCHAR(300) NOT NULL DEFAULT '',

    CONSTRAINT PK_Vehiculos
        PRIMARY KEY (Id),

    CONSTRAINT CK_Vehiculos_Kilometraje
        CHECK (Kilometraje >= 0)
) DEFAULT CHARSET = utf8mb4;
