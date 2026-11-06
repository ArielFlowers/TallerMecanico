using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using TallerMecanico.Contratos.Catalogos;
using TallerMecanico.Contratos.Ordenes;
using EstadoOrdenDominio = TallerMecanico.Dominio.Entidades.EstadoOrden;

namespace TallerMecanico.IntegrationTests;

[Trait("Area", "Backend")]
public sealed class OrdenesApiTests : IClassFixture<OrdenesApiFixture>
{
    private readonly OrdenesApiFixture _api;
    private PuertosOrdenesApi Puertos => _api.Puertos;

    public OrdenesApiTests(OrdenesApiFixture api)
    {
        _api = api;
        Puertos.Reiniciar();
    }

    [Fact]
    public async Task ListadoSugerenciasYDetalle_UsanParametrosYContratosSinExponerEntidades()
    {
        using var listado = await EnviarAsync(HttpMethod.Get,
            "api/ordenes?estado=Activa&fecha=2026-10-12&terminoBusqueda=123&ordenSeleccionadaId=1");
        Assert.Equal(HttpStatusCode.OK, listado.StatusCode);
        var orden = Assert.Single((await listado.Content.ReadFromJsonAsync<OrdenResumenDto[]>())!);
        Assert.Equal(1, orden.Id);
        Assert.Equal("123ABC", orden.Placa);
        Assert.Equal("Ana Arce Pérez", orden.NombreCliente);
        Assert.Equal(31m, orden.Total);
        Assert.Equal(EstadoOrden.Activa, orden.Estado);
        Assert.Equal(EstadoOrdenDominio.Activa, Puertos.EstadoRecibido);
        Assert.Equal(new DateOnly(2026, 10, 12), Puertos.FechaRecibida);
        Assert.Equal("123", Puertos.TerminoRecibido);
        Assert.Equal(1, Puertos.OrdenSeleccionadaRecibida);
        using var json = JsonDocument.Parse(await listado.Content.ReadAsStringAsync());
        Assert.False(json.RootElement[0].TryGetProperty("token", out _));
        Assert.False(json.RootElement[0].TryGetProperty("vehiculoId", out _));

        using var sugerencias = await EnviarAsync(HttpMethod.Get,
            "api/ordenes/sugerencias?termino=123&estado=Activa&fecha=2026-10-12");
        Assert.Equal(HttpStatusCode.OK, sugerencias.StatusCode);
        var sugerencia = Assert.Single((await sugerencias.Content.ReadFromJsonAsync<SugerenciaOrden[]>())!);
        Assert.Equal(1, sugerencia.Id);
        Assert.Equal("Orden #1", sugerencia.TextoPrincipal);
        Assert.Equal("123ABC · Ana Arce Pérez", sugerencia.TextoSecundario);
        Assert.Equal(EstadoOrdenDominio.Activa, Puertos.EstadoRecibido);
        Assert.Equal(new DateOnly(2026, 10, 12), Puertos.FechaRecibida);

        using var detalle = await EnviarAsync(HttpMethod.Get, "api/ordenes/1");
        Assert.Equal(HttpStatusCode.OK, detalle.StatusCode);
        var documento = (await detalle.Content.ReadFromJsonAsync<OrdenDetalleDto>())!;
        Assert.Equal("123ABC", documento.Placa);
        Assert.Equal("Ana Arce Pérez", documento.NombreCliente);
        Assert.Single(documento.Servicios);
        Assert.Single(documento.Productos);
        Assert.Equal("FIL-001", documento.Productos[0].Codigo);

        using var inexistente = await EnviarAsync(HttpMethod.Get, "api/ordenes/99");
        Assert.Equal(HttpStatusCode.NotFound, inexistente.StatusCode);
    }

    [Fact]
    public async Task FechaActualYComprobante_ConservanElRelojYElPdfDelServidor()
    {
        using var fecha = await EnviarAsync(HttpMethod.Get, "api/ordenes/fecha-actual");
        Assert.Equal(HttpStatusCode.OK, fecha.StatusCode);
        Assert.Equal(TimeProvider.System.GetUtcNow().ToOffset(TimeSpan.FromHours(-4)).Date,
            (await fecha.Content.ReadFromJsonAsync<DateTime>()).Date);

        using var comprobante = await EnviarAsync(HttpMethod.Get, "api/ordenes/1/comprobante");
        Assert.Equal(HttpStatusCode.OK, comprobante.StatusCode);
        Assert.Equal("application/pdf", comprobante.Content.Headers.ContentType?.MediaType);
        Assert.Equal("%PDF-prueba", Encoding.ASCII.GetString(await comprobante.Content.ReadAsByteArrayAsync()));

        using var inexistente = await EnviarAsync(HttpMethod.Get, "api/ordenes/99/comprobante");
        Assert.Equal(HttpStatusCode.NotFound, inexistente.StatusCode);
    }

    [Fact]
    public async Task Crear_ValidaAntesDeLaTransaccionYTraduceLaSolicitudYElResultado()
    {
        using var invalida = await EnviarAsync(HttpMethod.Post, "api/ordenes", new
        {
            vehiculoId = 0, mecanicoId = 0, fecha = DateTime.Today, token = Guid.Empty,
            lineas = Array.Empty<object>(), servicioId = (int?)null, servicioIds = Array.Empty<int>(),
            cantidadesServicios = Array.Empty<object>()
        });
        Assert.Equal(HttpStatusCode.OK, invalida.StatusCode);
        var fallo = (await invalida.Content.ReadFromJsonAsync<ResultadoOrdenDto>())!;
        Assert.False(fallo.Exito);
        Assert.Contains("VehiculoId", fallo.Errores.Keys);
        Assert.Equal(0, Puertos.Transacciones);

        Guid token = Guid.NewGuid();
        var solicitud = new
        {
            vehiculoId = 1, mecanicoId = 1, fecha = DateTime.Today, token,
            lineas = new[] { new { productoId = 1, cantidad = 2 } },
            servicioId = (int?)null, servicioIds = new[] { 1 }, cantidadesServicios = Array.Empty<object>()
        };
        using var creada = await EnviarAsync(HttpMethod.Post, "api/ordenes", solicitud);
        Assert.Equal(HttpStatusCode.OK, creada.StatusCode);
        var resultado = (await creada.Content.ReadFromJsonAsync<ResultadoOrdenDto>())!;
        Assert.True(resultado.Exito, string.Join("; ", resultado.Errores.Values));
        Assert.Equal(50m, resultado.Total);
        Assert.True(resultado.OrdenId > 0);
        Assert.Equal(8, Puertos.Productos.Single(producto => producto.Id == 1).Stock);
        Assert.Equal(1, Puertos.Confirmaciones);

        using var reenvio = await EnviarAsync(HttpMethod.Post, "api/ordenes", solicitud);
        var repetida = (await reenvio.Content.ReadFromJsonAsync<ResultadoOrdenDto>())!;
        Assert.True(repetida.Exito);
        Assert.True(repetida.EsReenvio);
        Assert.Equal(resultado.OrdenId, repetida.OrdenId);
        Assert.Equal(8, Puertos.Productos.Single(producto => producto.Id == 1).Stock);
    }

    [Fact]
    public async Task Anular_AplicaLasReglasDeRolYRestauraElStock()
    {
        using var recepcionista = await EnviarAsync(HttpMethod.Post, "api/ordenes/1/anular", rol: "Recepcionista");
        Assert.Equal(HttpStatusCode.OK, recepcionista.StatusCode);
        var denegada = (await recepcionista.Content.ReadFromJsonAsync<ResultadoOrdenDto>())!;
        Assert.False(denegada.Exito);
        Assert.Equal("Solo un administrador puede anular órdenes.", denegada.Errores[""]);
        Assert.Equal(10, Puertos.Productos.Single(producto => producto.Id == 1).Stock);

        using var administrador = await EnviarAsync(HttpMethod.Post, "api/ordenes/1/anular");
        Assert.Equal(HttpStatusCode.OK, administrador.StatusCode);
        var anulada = (await administrador.Content.ReadFromJsonAsync<ResultadoOrdenDto>())!;
        Assert.True(anulada.Exito);
        Assert.Equal(EstadoOrdenDominio.Anulada, Puertos.Ordenes.Single(orden => orden.Id == 1).Estado);
        Assert.Equal(12, Puertos.Productos.Single(producto => producto.Id == 1).Stock);

        using var repetida = await EnviarAsync(HttpMethod.Post, "api/ordenes/1/anular");
        var yaAnulada = (await repetida.Content.ReadFromJsonAsync<ResultadoOrdenDto>())!;
        Assert.False(yaAnulada.Exito);
        Assert.Equal("La orden ya está anulada.", yaAnulada.Errores["OrdenId"]);
    }

    [Theory]
    [InlineData("Administrador", true)]
    [InlineData("Recepcionista", true)]
    [InlineData("Invitado", false)]
    [InlineData("", false)]
    public async Task TodosLosEndpoints_RespetanLosRolesDelControlador(string rol, bool autorizado)
    {
        foreach (var (metodo, ruta, formulario) in Endpoints())
        {
            using var respuesta = await EnviarAsync(metodo, ruta, formulario, rol);
            if (autorizado)
                Assert.True(respuesta.IsSuccessStatusCode || respuesta.StatusCode == HttpStatusCode.BadRequest,
                    $"{rol}: {metodo} {ruta}: {(int)respuesta.StatusCode}");
            else
                Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("clave-incorrecta")]
    public async Task ClaveInternaAusenteOIncorrecta_RechazaTodosLosEndpoints(string? clave)
    {
        foreach (var (metodo, ruta, formulario) in Endpoints())
        {
            using var respuesta = await EnviarAsync(metodo, ruta, formulario, clave: clave);
            Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
        }
    }

    [Theory]
    [InlineData("api/ordenes", "null")]
    [InlineData("api/ordenes", "{malformado")]
    [InlineData("api/ordenes", "{\"vehiculoId\":\"no numérico\"}")]
    public async Task JsonInvalido_Devuelve400SinEscribir(string ruta, string json)
    {
        using var solicitud = Solicitud(HttpMethod.Post, ruta);
        solicitud.Content = new StringContent(json, Encoding.UTF8, "application/json");
        using var respuesta = await _api.Http.SendAsync(solicitud);
        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.Equal(0, Puertos.Transacciones);
    }

    [Theory]
    [InlineData("api/ordenes")]
    [InlineData("api/ordenes/sugerencias?termino=123")]
    [InlineData("api/ordenes/1")]
    public async Task FalloDePersistencia_UsaElFiltroRealYNoExponeDetalles(string ruta)
    {
        Puertos.ErrorLectura = Puertos.ErrorDetalle = new BaseDatosOrdenesApiException();
        using var respuesta = await EnviarAsync(HttpMethod.Get, ruta);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, respuesta.StatusCode);
        Assert.DoesNotContain("Detalle de persistencia", await respuesta.Content.ReadAsStringAsync());
    }

    private Task<HttpResponseMessage> EnviarAsync(HttpMethod metodo, string ruta, object? formulario = null,
        string rol = "Administrador", string? clave = ServiciosEnPrueba.ClaveInterna)
    {
        using var solicitud = Solicitud(metodo, ruta, rol, clave);
        if (formulario is not null) solicitud.Content = JsonContent.Create(formulario, formulario.GetType());
        return _api.Http.SendAsync(solicitud);
    }

    private static HttpRequestMessage Solicitud(HttpMethod metodo, string ruta, string rol = "Administrador",
        string? clave = ServiciosEnPrueba.ClaveInterna)
    {
        var solicitud = new HttpRequestMessage(metodo, ruta);
        if (clave is not null) solicitud.Headers.Add("X-Clave-Interna", clave);
        solicitud.Headers.Add("X-Usuario-Id", "17");
        solicitud.Headers.Add("X-Usuario-Nombre", Uri.EscapeDataString("recepción_prueba"));
        if (rol != "") solicitud.Headers.Add("X-Usuario-Rol", rol);
        return solicitud;
    }

    private static IEnumerable<(HttpMethod Metodo, string Ruta, object? Formulario)> Endpoints()
    {
        yield return (HttpMethod.Get, "api/ordenes", null);
        yield return (HttpMethod.Get, "api/ordenes/sugerencias?termino=123", null);
        yield return (HttpMethod.Get, "api/ordenes/fecha-actual", null);
        yield return (HttpMethod.Get, "api/ordenes/1", null);
        yield return (HttpMethod.Get, "api/ordenes/1/comprobante", null);
        yield return (HttpMethod.Post, "api/ordenes", new
        {
            vehiculoId = 1, mecanicoId = 1, fecha = DateTime.Today, token = Guid.NewGuid(),
            lineas = new[] { new { productoId = 1, cantidad = 1 } },
            servicioId = (int?)null, servicioIds = new[] { 1 }, cantidadesServicios = Array.Empty<object>()
        });
        yield return (HttpMethod.Post, "api/ordenes/1/anular", null);
    }
}
