CREATE TABLE IF NOT EXISTS Servicios (
    Id INT NOT NULL AUTO_INCREMENT,
    Nombre VARCHAR(100) NOT NULL,
    Descripcion VARCHAR(300) NOT NULL,
    Costo DECIMAL(10,2) NOT NULL,
    TiempoEstimadoHoras DECIMAL(6,2) NOT NULL,

    CONSTRAINT PK_Servicios
        PRIMARY KEY (Id),

    CONSTRAINT CK_Servicios_Costo
        CHECK (Costo > 0),

    CONSTRAINT CK_Servicios_TiempoEstimadoHoras
        CHECK (TiempoEstimadoHoras > 0)
);


CREATE TABLE IF NOT EXISTS HistorialCostoServicios (
    Id INT NOT NULL AUTO_INCREMENT,
    ServicioId INT NOT NULL,
    NombreServicio VARCHAR(100) NOT NULL,
    CostoAnterior DECIMAL(10,2) NOT NULL,
    CostoNuevo DECIMAL(10,2) NOT NULL,
    FechaCambio DATETIME NOT NULL,

    CONSTRAINT PK_HistorialCostoServicios
        PRIMARY KEY (Id)
);


DROP TRIGGER IF EXISTS TRG_Servicios_HistorialCosto;

DELIMITER //

CREATE TRIGGER TRG_Servicios_HistorialCosto
AFTER UPDATE ON Servicios
FOR EACH ROW
BEGIN
    IF OLD.Costo <> NEW.Costo THEN
        INSERT INTO HistorialCostoServicios (
            ServicioId,
            NombreServicio,
            CostoAnterior,
            CostoNuevo,
            FechaCambio
        )
        VALUES (
            NEW.Id,
            NEW.Nombre,
            OLD.Costo,
            NEW.Costo,
            NOW()
        );
    END IF;
END//

DELIMITER ;
