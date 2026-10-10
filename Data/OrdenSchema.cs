namespace TallerMecanico.Data;

internal static class OrdenSchema
{
    public const string CrearTablas = """
        CREATE TABLE IF NOT EXISTS Ordenes (
            Id INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
            VehiculoId INT NOT NULL,
            MecanicoId INT NOT NULL,
            Fecha DATETIME NOT NULL,
            Estado ENUM('Activa', 'Anulada') NOT NULL DEFAULT 'Activa',
            Total DECIMAL(18,2) NOT NULL DEFAULT 0,
            Token CHAR(36) NOT NULL,
            CreadoPor VARCHAR(100) NOT NULL,
            FechaCreacion DATETIME(6) NOT NULL,
            AnuladoPor VARCHAR(100) NULL,
            FechaAnulacion DATETIME(6) NULL,
            CONSTRAINT UQ_Ordenes_Token UNIQUE (Token),
            CONSTRAINT CK_Ordenes_Total CHECK (Total >= 0),
            CONSTRAINT CK_Ordenes_Anulacion CHECK (
                (Estado = 'Activa' AND AnuladoPor IS NULL AND FechaAnulacion IS NULL)
                OR (Estado = 'Anulada' AND AnuladoPor IS NOT NULL AND FechaAnulacion IS NOT NULL)
            ),
            CONSTRAINT FK_Ordenes_Vehiculos FOREIGN KEY (VehiculoId) REFERENCES Vehiculos(Id),
            CONSTRAINT FK_Ordenes_Mecanicos FOREIGN KEY (MecanicoId) REFERENCES Mecanicos(Id),
            INDEX IX_Ordenes_Fecha_Estado (Fecha, Estado)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

        CREATE TABLE IF NOT EXISTS DetalleOrdenes (
            Id INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
            OrdenId INT NOT NULL,
            ProductoId INT NOT NULL,
            Cantidad INT NOT NULL,
            PrecioUnitario DECIMAL(10,2) NOT NULL,
            Subtotal DECIMAL(18,2) NOT NULL,
            CONSTRAINT UQ_DetalleOrdenes_Orden_Producto UNIQUE (OrdenId, ProductoId),
            CONSTRAINT CK_DetalleOrdenes_Cantidad CHECK (Cantidad > 0),
            CONSTRAINT CK_DetalleOrdenes_Precio CHECK (PrecioUnitario > 0),
            CONSTRAINT CK_DetalleOrdenes_Subtotal CHECK (Subtotal = Cantidad * PrecioUnitario),
            CONSTRAINT FK_DetalleOrdenes_Ordenes FOREIGN KEY (OrdenId) REFERENCES Ordenes(Id) ON DELETE CASCADE,
            CONSTRAINT FK_DetalleOrdenes_Productos FOREIGN KEY (ProductoId) REFERENCES Productos(Id)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
        """;
}
