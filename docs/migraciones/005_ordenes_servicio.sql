-- MySQL 8. Servicio obligatorio por orden (aplicación), columnas NULL para no romper órdenes históricas.
-- Idempotente: ejecutar con COLUMN/FK inexistentes; verificar antes en INFORMATION_SCHEMA si se repite.
ALTER TABLE Ordenes
    ADD COLUMN ServicioId INT NULL,
    ADD COLUMN CostoServicio DECIMAL(10,2) NULL,
    ADD CONSTRAINT FK_Ordenes_Servicios FOREIGN KEY (ServicioId) REFERENCES Servicios(Id),
    ADD CONSTRAINT CK_Ordenes_Servicio CHECK (
        (ServicioId IS NULL AND CostoServicio IS NULL)
        OR (ServicioId IS NOT NULL AND CostoServicio > 0)
    );
