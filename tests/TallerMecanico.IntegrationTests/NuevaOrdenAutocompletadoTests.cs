using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Logging.Abstractions;
using TallerMecanico.Aplicacion.Dto;
using TallerMecanico.Aplicacion.Fachadas;
using TallerMecanico.Aplicacion.Servicios;
using TallerMecanico.Aplicacion.Validaciones;
using TallerMecanico.Infraestructura.Persistencia;
using TallerMecanico.Infraestructura.Persistencia.Fabricas;
using TallerMecanico.Infraestructura.Persistencia.MySql;
using TallerMecanico.Infraestructura.Sesion;
using PaginaOrden = TallerMecanico.Pages.Ordenes.CrearModel;

namespace TallerMecanico.IntegrationTests;

public sealed class NuevaOrdenAutocompletadoTests
{
    [MySqlAisladoFact]
    public async Task Consultas_ReutilizanPrefijosPalabrasTildesLimitesSqlYSoloLectura()
    {
        using var aislada = new MySqlAisladoFixture();
        Preparar(aislada, masivos: true);
        var pagina = CrearPagina(aislada);
        string antes = Instantanea(aislada);
        Assert.Single(Sugerencias(await pagina.OnGetSugerenciasVehiculosAsync("toy cru")));
        Assert.Single(Sugerencias(await pagina.OnGetSugerenciasServiciosAsync("lubrica")));
        Assert.Single(Sugerencias(await pagina.OnGetSugerenciasMecanicosAsync("jose munoz")));
        Assert.Single(Sugerencias(await pagina.OnGetSugerenciasProductosAsync("filtro acei")));
        Assert.Empty(Sugerencias(await pagina.OnGetSugerenciasVehiculosAsync("oyota")));
        Assert.Empty(Sugerencias(await pagina.OnGetSugerenciasProductosAsync("iltro")));
        Assert.Equal("CI: 1234567", Assert.Single(Sugerencias(await pagina.OnGetSugerenciasMecanicosAsync("123"))).GetProperty("textoSecundario").GetString());
        foreach (var consulta in Consultas(pagina))
        {
            Assert.Equal(10, Sugerencias(await consulta("prue")).Length);
            foreach (string termino in new[] { "", " ", "a", "%%", "__", "' OR 1=1 --", new string('a', 101) })
                Assert.Empty(Sugerencias(await consulta(termino)));
        }
        Assert.IsType<JsonResult>(pagina.OnGetVehiculoSeleccionado(1));
        Assert.IsType<JsonResult>(pagina.OnGetServicioSeleccionado(1));
        Assert.IsType<NotFoundResult>(pagina.OnGetServicioSeleccionado(-1));
        Assert.IsType<NotFoundResult>(pagina.OnGetVehiculoSeleccionado(int.MaxValue));
        Assert.Equal(antes, Instantanea(aislada));
    }

    [MySqlAisladoFact]
    public void Borradores_RestauranIdsTextosYDetalleAlSalirHaciaLosTresCrud()
    {
        using var aislada = new MySqlAisladoFixture();
        Preparar(aislada);
        var sesion = new SesionPrueba();
        var pagina = CrearPagina(aislada, sesion);
        pagina.OnGet();
        pagina.Formulario.VehiculoId = 1; pagina.Formulario.MecanicoId = 1; pagina.Formulario.ServicioId = 1;
        pagina.Formulario.Fecha = new DateTime(2026, 10, 12);
        pagina.ProductoId = 1; pagina.Cantidad = 2;
        var agregado = Assert.IsType<RedirectToPageResult>(pagina.OnPostAgregarLinea());
        Guid id = (Guid)agregado.RouteValues!["BorradorId"]!;
        foreach (string destino in new[] { "Cliente", "Vehiculo", "Producto" })
        {
            var restaurada = CrearPagina(aislada, sesion);
            restaurada.BorradorId = id;
            restaurada.OnGet();
            Assert.Equal("123ABC", restaurada.BuscarVehiculo);
            Assert.Equal("Cambio de Aceite", restaurada.TextoServicio);
            Assert.Equal("José Muñoz", restaurada.TextoMecanico);
            Assert.Equal(1, restaurada.Formulario.VehiculoId);
            Assert.Equal(1, restaurada.Formulario.MecanicoId);
            Assert.Equal(1, restaurada.Formulario.ServicioId);
            Assert.Equal(2, Assert.Single(restaurada.Lineas).Cantidad);
            Assert.Equal(50m, restaurada.TotalEstimado);
            var retorno = Assert.IsType<RedirectToPageResult>(destino switch
            {
                "Cliente" => restaurada.OnPostNuevoCliente(),
                "Vehiculo" => restaurada.OnPostNuevoVehiculo(),
                _ => restaurada.OnPostNuevoProducto()
            });
            Assert.Equal($"/{(destino == "Cliente" ? "Clientes" : destino == "Vehiculo" ? "Vehiculos" : "Productos")}/Index", retorno.PageName);
            id = (Guid)retorno.RouteValues!["OrdenBorradorId"]!;
        }
        var trasCliente = CrearPagina(aislada, sesion);
        trasCliente.BorradorId = id; trasCliente.ClienteNuevoId = 1; trasCliente.OnGet();
        Assert.Equal(1, trasCliente.Formulario.ClienteNuevoId);
        Assert.Single(trasCliente.Lineas);
        Assert.Equal(0L, Convert.ToInt64(aislada.Escalar("SELECT COUNT(*) FROM Ordenes;")));
    }

    [MySqlAisladoFact]
    public void Productos_AgregarRepetirActualizarQuitarYErroresNoDescuentanStock()
    {
        using var aislada = new MySqlAisladoFixture();
        Preparar(aislada);
        var pagina = CrearPagina(aislada);
        pagina.OnGet();
        pagina.Formulario.VehiculoId = 1; pagina.Formulario.MecanicoId = 1; pagina.Formulario.ServicioId = 1;
        pagina.ProductoId = 1; pagina.Cantidad = 2; pagina.OnPostAgregarLinea();
        pagina.ProductoId = 2; pagina.Cantidad = 3; pagina.OnPostAgregarLinea();
        pagina.ProductoId = 1; pagina.Cantidad = 4; pagina.OnPostAgregarLinea();
        Assert.Equal(2, pagina.Formulario.Lineas.Count);
        Assert.Equal(4, pagina.Formulario.Lineas.Single(linea => linea.ProductoId == 1).Cantidad);
        pagina.OnPostActualizarCantidad(1, 2);
        pagina.OnPostQuitarLinea(2);
        pagina.Cantidad = 0;
        Assert.IsType<PageResult>(pagina.OnPostAgregarLinea());
        Assert.NotNull(pagina.VehiculoSeleccionado);
        Assert.Equal("José Muñoz", pagina.TextoMecanico);
        Assert.Equal("Cambio de Aceite", pagina.TextoServicio);
        Assert.Equal(2, Assert.Single(pagina.Lineas).Cantidad);
        Assert.Equal(20L, Convert.ToInt64(aislada.Escalar("SELECT SUM(Stock) FROM Productos;")));
        Assert.Equal(0L, Convert.ToInt64(aislada.Escalar("SELECT COUNT(*) FROM Ordenes;")));
    }

    [MySqlAisladoFact]
    public void Guardar_ValidaIdsStockPreciosDelServidorYConservaIdempotencia()
    {
        using var aislada = new MySqlAisladoFixture();
        Preparar(aislada);
        var sesion = new SesionPrueba();
        var pagina = CrearPagina(aislada, sesion);
        pagina.OnGet();
        pagina.Formulario.Lineas.Add(new() { ProductoId = 1, Cantidad = 1, Precio = 0.01m });
        Assert.IsType<PageResult>(pagina.OnPostGuardar());
        Assert.Contains("Formulario.VehiculoId", pagina.ModelState.Keys);
        Assert.Contains("Formulario.MecanicoId", pagina.ModelState.Keys);
        Assert.Contains("Formulario.ServicioId", pagina.ModelState.Keys);
        pagina.ModelState.Clear();
        pagina.Formulario.VehiculoId = 2; pagina.Formulario.MecanicoId = 1; pagina.Formulario.ServicioId = 1;
        Assert.IsType<PageResult>(pagina.OnPostGuardar());
        Assert.Contains("cliente", pagina.ModelState["Formulario.VehiculoId"]!.Errors.Single().ErrorMessage);
        pagina.ModelState.Clear();
        pagina.Formulario.VehiculoId = int.MaxValue;
        Assert.IsType<PageResult>(pagina.OnPostGuardar());
        pagina.ModelState.Clear();
        pagina.Formulario.VehiculoId = 1;
        pagina.Formulario.Lineas = [new() { ProductoId = 1, Cantidad = 11, Precio = 0.01m }];
        Assert.IsType<PageResult>(pagina.OnPostGuardar());
        Assert.Equal(0L, Convert.ToInt64(aislada.Escalar("SELECT COUNT(*) FROM Ordenes;")));
        pagina.ModelState.Clear();
        pagina.Formulario.Lineas = [new() { ProductoId = 1, Cantidad = 2, Precio = 0.01m }, new() { ProductoId = 2, Cantidad = 3, Precio = 0.01m }];
        Assert.IsType<RedirectToPageResult>(pagina.OnPostGuardar());
        Assert.Equal(71.60m, Convert.ToDecimal(aislada.Escalar("SELECT Total FROM Ordenes;")));
        Assert.Equal(15L, Convert.ToInt64(aislada.Escalar("SELECT SUM(Stock) FROM Productos;")));
        Assert.IsType<RedirectToPageResult>(pagina.OnPostGuardar());
        Assert.Equal(1L, Convert.ToInt64(aislada.Escalar("SELECT COUNT(*) FROM Ordenes;")));
        Assert.Equal(15L, Convert.ToInt64(aislada.Escalar("SELECT SUM(Stock) FROM Productos;")));
    }

    internal static PaginaOrden CrearPagina(MySqlAisladoFixture aislada, SesionPrueba? sesion = null)
    {
        var contexto = new DefaultHttpContext { Session = sesion ?? new SesionPrueba() };
        var acceso = new HttpContextAccessor { HttpContext = contexto };
        var usuario = new UsuarioPrueba();
        var ordenes = new OrdenServicioService(new MySqlUnidadTrabajoOrden(aislada.Factory, NullLogger<MySqlUnidadTrabajoOrden>.Instance),
            usuario, new ValidacionOrdenes(), TimeProvider.System, NullLogger<OrdenServicioService>.Instance);
        var pagina = new PaginaOrden(new OrdenServicioFacade(ordenes, new TokenOrdenSesion(acceso, usuario, TimeProvider.System)),
            new BorradorOrdenSesion(acceso, usuario), VehiculosAutocompletadoTests.CrearServicio(aislada),
            new MecanicoService(new CreadorMecanico(aislada.Factory), new ValidacionMecanicos(), usuario),
            ProductosAutocompletadoTests.CrearServicio(aislada), ClientesAutocompletadoTests.CrearServicio(aislada),
            ServiciosAutocompletadoTests.CrearServicio(aislada))
        {
            PageContext = new PageContext { HttpContext = contexto }, TempData = new TempDataDictionary(contexto, new Temporales())
        };
        return pagina;
    }

    internal static void Preparar(MySqlAisladoFixture aislada, bool masivos = false)
    {
        aislada.Ejecutar("INSERT INTO Clientes (Ci,Nombres,PrimerApellido,SegundoApellido,Celular) VALUES ('1234567','Ana','Arce','Pérez','71234567');");
        aislada.Ejecutar("INSERT INTO Vehiculos (Placa,Marca,Modelo,ClienteId,Kilometraje) VALUES ('123ABC','Toyota','Land Cruiser',1,100),('456DEF','Toyota','Corolla',NULL,100);");
        aislada.Ejecutar("INSERT INTO Mecanicos (Ci,Nombres,PrimerApellido,SegundoApellido,Genero,Especialidad,Celular) VALUES ('1234567','José','Muñoz','','Masculino','Sin Especialidad','71234567');");
        aislada.Ejecutar("INSERT INTO Servicios (Nombre,Descripcion,Costo,TiempoEstimadoHoras) VALUES ('Cambio de Aceite','Lubricación del motor',25,1);");
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

    private static JsonElement[] Sugerencias(JsonResult resultado) => JsonSerializer.SerializeToElement(resultado.Value,
        new JsonSerializerOptions(JsonSerializerDefaults.Web)).EnumerateArray().ToArray();

    private static Func<string?, Task<JsonResult>>[] Consultas(PaginaOrden pagina) =>
        [pagina.OnGetSugerenciasVehiculosAsync, pagina.OnGetSugerenciasServiciosAsync, pagina.OnGetSugerenciasMecanicosAsync, pagina.OnGetSugerenciasProductosAsync];

    private sealed class Temporales : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
        public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
    }
}
