using System.Text.Json;

namespace TallerMecanico.IntegrationTests;

[Trait("Area", "Datos")]
public sealed class NuevaOrdenAutocompletadoTests
{
    internal static void Preparar(MySqlAisladoFixture aislada, bool masivos = false)
    {
        aislada.Ejecutar("INSERT INTO Clientes (Ci,Nombres,PrimerApellido,SegundoApellido,Celular) VALUES ('1234567','Ana','Arce','Pérez','71234567');");
        aislada.Ejecutar("INSERT INTO Vehiculos (Placa,Marca,Modelo,ClienteId,Kilometraje) VALUES ('123ABC','Toyota','Land Cruiser',1,100),('456DEF','Toyota','Corolla',NULL,100);");
        aislada.Ejecutar("INSERT INTO Mecanicos (Ci,Nombres,PrimerApellido,SegundoApellido,Genero,Especialidad,Celular) VALUES ('1234567','José','Muñoz','','Masculino','Sin Especialidad','71234567');");
        aislada.Ejecutar("INSERT INTO Servicios (CodigoCatalogo,Nombre,Descripcion,Costo,TiempoEstimadoHoras) VALUES ('CAMBIO_ACEITE','Cambio de Aceite','Lubricación del motor',25,1);");
        aislada.Ejecutar("INSERT INTO Productos (Codigo,Nombre,Precio,Stock,StockMinimo) VALUES ('FIL-001','Filtro de Aceite',12.50,10,0),('BUJ-001','Bujía de Iridio',7.20,10,0);");
        if (!masivos) return;
        for (int indice = 0; indice < 13; indice++)
        {
            aislada.Ejecutar("INSERT INTO Vehiculos (Placa,Marca,Modelo,ClienteId,Kilometraje) VALUES (@Placa,'Citroën','Prueba',1,100);", ("@Placa", $"{800 + indice}ABC"));
            aislada.Ejecutar("INSERT INTO Mecanicos (Ci,Nombres,PrimerApellido,SegundoApellido,Genero,Especialidad,Celular) VALUES (@Ci,'Prueba','Arce','','Femenino','Sin Especialidad','71234567');", ("@Ci", (8900000 + indice).ToString()));
            aislada.Ejecutar("INSERT INTO Servicios (Nombre,Descripcion,Costo,TiempoEstimadoHoras) VALUES ('Inspección de Prueba','Revisión general',40,1);");
            aislada.Ejecutar("INSERT INTO Productos (Codigo,Nombre,Precio,Stock,StockMinimo) VALUES (@Codigo,'Repuesto de Prueba',2,10,0);", ("@Codigo", $"TMP-{indice:00}"));
        }
    }

    internal static string Instantanea(MySqlAisladoFixture aislada) => JsonSerializer.Serialize(new[]
    {
        aislada.Escalar("SELECT COUNT(*) FROM Ordenes;"),
        aislada.Escalar("SELECT GROUP_CONCAT(CONCAT_WS('|',Id,Stock,Precio,CreadoPor,FechaCreacion,ModificadoPor,FechaModificacion) ORDER BY Id) FROM Productos;"),
        aislada.Escalar("SELECT COUNT(*) FROM DetalleOrdenes;")
    });
}
