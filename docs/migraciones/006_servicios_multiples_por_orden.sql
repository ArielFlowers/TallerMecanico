-- MySQL 8. Conserva los servicios de órdenes existentes y guarda importes históricos.
-- DatabaseInitializer crea esta tabla y migra los registros al iniciar.
CREATE TABLE IF NOT EXISTS DetalleServiciosOrdenes (
    Id INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
    OrdenId INT NOT NULL,
    ServicioId INT NOT NULL,
    NombreServicio VARCHAR(100) NOT NULL,
    DescripcionServicio VARCHAR(300) NOT NULL,
    Costo DECIMAL(10,2) NOT NULL,
    TiempoEstimadoHoras DECIMAL(6,2) NOT NULL,
    CONSTRAINT UQ_DetalleServiciosOrdenes_Orden_Servicio UNIQUE (OrdenId, ServicioId),
    CONSTRAINT CK_DetalleServiciosOrdenes_Costo CHECK (Costo > 0),
    CONSTRAINT CK_DetalleServiciosOrdenes_Tiempo CHECK (TiempoEstimadoHoras > 0),
    CONSTRAINT FK_DetalleServiciosOrdenes_Ordenes FOREIGN KEY (OrdenId) REFERENCES Ordenes(Id) ON DELETE CASCADE,
    CONSTRAINT FK_DetalleServiciosOrdenes_Servicios FOREIGN KEY (ServicioId) REFERENCES Servicios(Id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

INSERT IGNORE INTO DetalleServiciosOrdenes
    (OrdenId, ServicioId, NombreServicio, DescripcionServicio, Costo, TiempoEstimadoHoras)
SELECT Orden.Id, Servicio.Id, Servicio.Nombre, Servicio.Descripcion,
       COALESCE(Orden.CostoServicio, Servicio.Costo), Servicio.TiempoEstimadoHoras
FROM Ordenes AS Orden
JOIN Servicios AS Servicio ON Servicio.Id = Orden.ServicioId
WHERE Orden.ServicioId IS NOT NULL;
