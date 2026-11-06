using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Logging.Abstractions;
using TallerMecanico.Aplicacion.Fachadas;
using TallerMecanico.Aplicacion.Servicios;
using TallerMecanico.Aplicacion.Validaciones;
using TallerMecanico.Infraestructura.Persistencia;
using TallerMecanico.Infraestructura.Persistencia.Fabricas;
using TallerMecanico.Infraestructura.Persistencia.MySql;

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

    // Las mismas consultas que la nueva orden pide a la API.
    [MySqlAisladoFact]
    public async Task Consultas_ReutilizanPrefijosPalabrasTildesLimitesSqlYSoloLectura()
    {
        using var aislada = new MySqlAisladoFixture();
        Preparar(aislada, masivos: true);
        var vehiculos = VehiculosAutocompletadoTests.CrearServicio(aislada);
        var mecanicos = MecanicosAutocompletadoTests.CrearServicio(aislada);
        var productos = ProductosAutocompletadoTests.CrearServicio(aislada);
        var servicios = ServiciosAutocompletadoTests.CrearServicio(aislada);
        string antes = Instantanea(aislada);

        Assert.Single(await vehiculos.SugerirAsync("toy cru"));
        Assert.Single(await servicios.SugerirCatalogoAsync("lubrica"));
        Assert.Single(await mecanicos.SugerirAsync("jose munoz", incluirEspecialidad: false));
        Assert.Single(await productos.SugerirAsync("filtro acei"));
        Assert.Empty(await vehiculos.SugerirAsync("oyota"));
        Assert.Empty(await productos.SugerirAsync("iltro"));
        Assert.Equal("CI: 1234567", Assert.Single(await mecanicos.SugerirAsync("123", incluirEspecialidad: false)).TextoSecundario);

        // Sin término la página muestra las listas completas; los productos exigen búsqueda.
        Assert.Equal(15, vehiculos.GetAll().Count);
        Assert.Equal(14, mecanicos.Obtener().Count);
        Assert.Single(servicios.ObtenerCatalogo());
        foreach (string? termino in new string?[] { null, "", " " })
            Assert.Empty(await productos.SugerirAsync(termino));

        // Los servicios libres de la fixture no se ofrecen como servicios de catálogo.
        Assert.Empty(await servicios.SugerirCatalogoAsync("prue"));
        Assert.Equal(10, (await vehiculos.SugerirAsync("prue")).Count);
        Assert.Equal(10, (await mecanicos.SugerirAsync("prue", incluirEspecialidad: false)).Count);
        Assert.Equal(10, (await productos.SugerirAsync("prue")).Count);
        foreach (string termino in new[] { "a", "%%", "__", "' OR 1=1 --", new string('a', 101) })
        {
            Assert.Empty(await vehiculos.SugerirAsync(termino));
            Assert.Empty(await mecanicos.SugerirAsync(termino, incluirEspecialidad: false));
            Assert.Empty(await productos.SugerirAsync(termino));
        }

        Assert.NotNull(vehiculos.GetById(1));
        Assert.NotNull(servicios.ObtenerDelCatalogo(1));
        Assert.Null(servicios.ObtenerDelCatalogo(-1));
        Assert.Null(vehiculos.GetById(int.MaxValue));
        Assert.Equal(antes, Instantanea(aislada));
    }

    internal static string Instantanea(MySqlAisladoFixture aislada) => JsonSerializer.Serialize(new[]
    {
        aislada.Escalar("SELECT COUNT(*) FROM Ordenes;"),
        aislada.Escalar("SELECT GROUP_CONCAT(CONCAT_WS('|',Id,Stock,Precio,CreadoPor,FechaCreacion,ModificadoPor,FechaModificacion) ORDER BY Id) FROM Productos;"),
        aislada.Escalar("SELECT COUNT(*) FROM DetalleOrdenes;")
    });

    private static JsonElement[] Sugerencias(JsonResult resultado) => JsonSerializer.SerializeToElement(resultado.Value,
        new JsonSerializerOptions(JsonSerializerDefaults.Web)).EnumerateArray().ToArray();

    private sealed class Temporales : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
        public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
    }
}
