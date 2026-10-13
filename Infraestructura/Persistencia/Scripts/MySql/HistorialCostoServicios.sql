CREATE TABLE IF NOT EXISTS HistorialCostoServicios (
    Id INT NOT NULL AUTO_INCREMENT,
    ServicioId INT NOT NULL,
    NombreServicio VARCHAR(100) NOT NULL,
    CostoAnterior DECIMAL(10,2) NOT NULL,
    CostoNuevo DECIMAL(10,2) NOT NULL,
    FechaCambio DATETIME NOT NULL,

    CONSTRAINT PK_HistorialCostoServicios
        PRIMARY KEY (Id)
) DEFAULT CHARSET = utf8mb4;

-- MySQL no soporta CREATE TRIGGER IF NOT EXISTS:
-- se ejecuta DROP + CREATE como comandos separados (sin DELIMITER).
DROP TRIGGER IF EXISTS TRG_Servicios_HistorialCosto;

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
END;
