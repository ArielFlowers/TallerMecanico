using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using TallerMecanico.Aplicacion.Errores;
using TallerMecanico.Contratos;
using TallerMecanico.Dominio.Entidades;

namespace TallerMecanico.IntegrationTests;

[Trait("Area", "Backend")]
public sealed class ClientesVehiculosApiTests : IClassFixture<ClientesVehiculosApiFixture>
{
    private readonly ClientesVehiculosApiFixture _api;
    private PuertosCatalogosApi Puertos => _api.Puertos;

    public ClientesVehiculosApiTests(ClientesVehiculosApiFixture api)
    {
        _api = api;
        Puertos.Reiniciar();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ListadoYBusqueda_UsanParametroDeS11YDtoSinExponerEntidades(bool vehiculos)
    {
        string ruta = vehiculos ? "api/vehiculos" : "api/clientes";
        using var listado = await EnviarAsync(HttpMethod.Get, ruta);
        Assert.Equal(HttpStatusCode.OK, listado.StatusCode);
        using var json = JsonDocument.Parse(await listado.Content.ReadAsStringAsync());
        Assert.Equal(vehiculos ? 1 : 2, json.RootElement.GetArrayLength());
        Assert.False(json.RootElement[0].TryGetProperty("modificadoPor", out _));

        string termino = vehiculos ? "  Corolla  " : "  José  ";
        using var busqueda = await EnviarAsync(HttpMethod.Get, $"{ruta}?terminoBusqueda={Uri.EscapeDataString(termino)}");
        Assert.Equal(HttpStatusCode.OK, busqueda.StatusCode);
        if (vehiculos)
        {
            Assert.Equal("Corolla", Puertos.BusquedaVehiculos);
            Assert.Equal("123ABC", Assert.Single((await busqueda.Content.ReadFromJsonAsync<VehiculoDto[]>())!).Placa);
        }
        else
        {
            Assert.Equal("José", Puertos.BusquedaClientes);
            Assert.Equal("Muñoz", Assert.Single((await busqueda.Content.ReadFromJsonAsync<ClienteDto[]>())!).PrimerApellido);
        }
        using var vacio = await EnviarAsync(HttpMethod.Get, $"{ruta}?terminoBusqueda=%20%20");
        using var datos = JsonDocument.Parse(await vacio.Content.ReadAsStringAsync());
        Assert.Equal(vehiculos ? 1 : 2, datos.RootElement.GetArrayLength());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Sugerencias_ConservanContratoYNormalizacionDelServicio(bool vehiculos)
    {
        string ruta = vehiculos ? "api/vehiculos" : "api/clientes";
        using var respuesta = await EnviarAsync(HttpMethod.Get, $"{ruta}/sugerencias?termino=%20Ana%20%20Prueba%20");
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        if (vehiculos)
        {
            var sugerencia = Assert.Single((await respuesta.Content.ReadFromJsonAsync<SugerenciaVehiculo[]>())!);
            Assert.Equal(7, sugerencia.Id);
            Assert.Equal("Toyota Corolla", sugerencia.TextoSecundario);
            Assert.Equal("Ana Prueba", Puertos.SugerenciasVehiculos);
        }
        else
        {
            var sugerencia = Assert.Single((await respuesta.Content.ReadFromJsonAsync<SugerenciaCliente[]>())!);
            Assert.Equal("CI: 1234567-1A", sugerencia.TextoSecundario);
            Assert.Equal("Ana Prueba", Puertos.SugerenciasClientes);
        }
    }

    [Theory]
    [InlineData("api/clientes", "")]
    [InlineData("api/clientes", "a")]
    [InlineData("api/vehiculos", "")]
    [InlineData("api/vehiculos", "a")]
    public async Task SugerenciasCortas_NoConsultanPersistencia(string ruta, string termino)
    {
        using var respuesta = await EnviarAsync(HttpMethod.Get, $"{ruta}/sugerencias?termino={termino}");
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.Empty((await respuesta.Content.ReadFromJsonAsync<JsonElement[]>())!);
        Assert.Equal(0, Puertos.Consultas);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ObtenerPorId_UsaMapeosYDevuelve404SiNoExiste(bool vehiculos)
    {
        string ruta = vehiculos ? "api/vehiculos" : "api/clientes";
        using var respuesta = await EnviarAsync(HttpMethod.Get, $"{ruta}/7");
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        if (vehiculos)
        {
            var vehiculo = (await respuesta.Content.ReadFromJsonAsync<VehiculoDto>())!;
            Assert.Equal(7, vehiculo.ClienteId);
            Assert.Equal(1200, vehiculo.Kilometraje);
            Assert.Equal("Vehículo de prueba", vehiculo.Observaciones);
        }
        else
        {
            var cliente = (await respuesta.Content.ReadFromJsonAsync<ClienteDto>())!;
            Assert.Equal("1A", cliente.ComplementoCi);
            Assert.Equal("autor_original", cliente.CreadoPor);
            Assert.Equal(new DateTime(2026, 11, 1), cliente.FechaCreacion);
        }
        using var inexistente = await EnviarAsync(HttpMethod.Get, $"{ruta}/99");
        Assert.Equal(HttpStatusCode.NotFound, inexistente.StatusCode);
    }

    [Fact]
    public async Task CrearCliente_NormalizaValidaYDevuelveIdParaRetornarALaOrden()
    {
        var formulario = ClienteFormulario(); formulario.Id = 999;
        using var respuesta = await EnviarAsync(HttpMethod.Post, "api/clientes", formulario);
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var resultado = (await respuesta.Content.ReadFromJsonAsync<ResultadoFormulario<ClienteFormViewModel>>())!;
        Assert.Empty(resultado.Errores);
        Assert.Equal("María Fernanda", resultado.Formulario.Nombres);
        Assert.Equal("Pérez", resultado.Formulario.PrimerApellido);
        Assert.Equal("Rojas", resultado.Formulario.SegundoApellido);
        Assert.Equal("1A", resultado.Formulario.ComplementoCi);
        Assert.Equal("71234567", resultado.Formulario.Celular);
        Assert.True(resultado.Formulario.Id > 0);
        Assert.NotEqual(999, resultado.Formulario.Id);
        var cliente = Puertos.Clientes.Single(cliente => cliente.Id == resultado.Formulario.Id);
        Assert.Equal("recepción_prueba", cliente.CreadoPor);
        Assert.NotEqual(default, cliente.FechaCreacion);
        Assert.Equal(1, Puertos.Escrituras);
    }

    [Fact]
    public async Task CrearVehiculo_ConservaIdDeAplicacionYPermiteRecuperarloComoS11()
    {
        var formulario = VehiculoFormulario(); formulario.Id = 999;
        using var respuesta = await EnviarAsync(HttpMethod.Post, "api/vehiculos", formulario);
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var resultado = (await respuesta.Content.ReadFromJsonAsync<ResultadoFormulario<VehiculoFormViewModel>>())!;
        Assert.Empty(resultado.Errores);
        Assert.Equal("456DEF", resultado.Formulario.Placa);
        Assert.Equal("Toyota", resultado.Formulario.Marca);
        Assert.Equal("Corolla", resultado.Formulario.Modelo);
        Assert.Equal(0, resultado.Formulario.Id);
        using var consulta = await EnviarAsync(HttpMethod.Get, $"api/vehiculos?terminoBusqueda={resultado.Formulario.Placa}");
        var vehiculo = Assert.Single((await consulta.Content.ReadFromJsonAsync<VehiculoDto[]>())!);
        Assert.True(vehiculo.Id > 0);
        Assert.NotEqual(999, vehiculo.Id);
        Assert.Equal(7, vehiculo.ClienteId);
        Assert.Equal("recepción_prueba", Puertos.Vehiculos.Single(actual => actual.Id == vehiculo.Id).CreadoPor);
        Assert.Equal(1, Puertos.Escrituras);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Actualizar_UsaIdDeRutaYConservaAuditoriaDeCreacion(bool vehiculos)
    {
        if (vehiculos)
        {
            var formulario = VehiculoFormulario(); formulario.Id = 999;
            using var respuesta = await EnviarAsync(HttpMethod.Put, "api/vehiculos/7", formulario);
            Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
            var resultado = (await respuesta.Content.ReadFromJsonAsync<ResultadoFormulario<VehiculoFormViewModel>>())!;
            Assert.Empty(resultado.Errores);
            Assert.Equal(7, resultado.Formulario.Id);
            var vehiculo = Assert.Single(Puertos.Vehiculos);
            Assert.Equal("456DEF", vehiculo.Placa);
            Assert.Equal("autor_original", vehiculo.CreadoPor);
            Assert.Equal(new DateTime(2026, 11, 1), vehiculo.FechaCreacion);
            Assert.Equal("recepción_prueba", vehiculo.ModificadoPor);
            Assert.NotNull(vehiculo.FechaModificacion);
        }
        else
        {
            var formulario = ClienteFormulario(); formulario.Id = 8;
            using var respuesta = await EnviarAsync(HttpMethod.Put, "api/clientes/7", formulario);
            Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
            var resultado = (await respuesta.Content.ReadFromJsonAsync<ResultadoFormulario<ClienteFormViewModel>>())!;
            Assert.Empty(resultado.Errores);
            Assert.Equal(7, resultado.Formulario.Id);
            var cliente = Puertos.Clientes.Single(cliente => cliente.Id == 7);
            Assert.Equal("María Fernanda", cliente.Nombres);
            Assert.Equal("autor_original", cliente.CreadoPor);
            Assert.Equal(new DateTime(2026, 11, 1), cliente.FechaCreacion);
            Assert.Equal("recepción_prueba", cliente.ModificadoPor);
            Assert.Equal("José", Puertos.Clientes.Single(cliente => cliente.Id == 8).Nombres);
        }
        Assert.Equal(1, Puertos.Escrituras);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Validacion_DevuelveErroresPorCampoCon200ComoEsperaApiBase(bool vehiculos)
    {
        using var respuesta = await EnviarAsync(HttpMethod.Post, vehiculos ? "api/vehiculos" : "api/clientes",
            vehiculos ? new VehiculoFormViewModel() : new ClienteFormViewModel());
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        if (vehiculos)
        {
            var resultado = (await respuesta.Content.ReadFromJsonAsync<ResultadoFormulario<VehiculoFormViewModel>>())!;
            Assert.Equal("La placa es obligatoria.", resultado.Errores[nameof(VehiculoFormViewModel.Placa)]);
            Assert.Contains(nameof(VehiculoFormViewModel.Marca), resultado.Errores.Keys);
            Assert.Contains(nameof(VehiculoFormViewModel.ClienteId), resultado.Errores.Keys);
        }
        else
        {
            var resultado = (await respuesta.Content.ReadFromJsonAsync<ResultadoFormulario<ClienteFormViewModel>>())!;
            Assert.Equal("El CI es obligatorio.", resultado.Errores[nameof(ClienteFormViewModel.Ci)]);
            Assert.Contains(nameof(ClienteFormViewModel.Nombres), resultado.Errores.Keys);
            Assert.Contains(nameof(ClienteFormViewModel.PrimerApellido), resultado.Errores.Keys);
            Assert.Contains(nameof(ClienteFormViewModel.SegundoApellido), resultado.Errores.Keys);
            Assert.Contains(nameof(ClienteFormViewModel.Celular), resultado.Errores.Keys);
        }
        Assert.Equal(0, Puertos.Escrituras);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Duplicados_ConservanMensajeYClaveDelCampo(bool vehiculos)
    {
        object formulario;
        if (vehiculos) { var vehiculo = VehiculoFormulario(); vehiculo.Placa = "123ABC"; formulario = vehiculo; }
        else { var cliente = ClienteFormulario(); cliente.Ci = "1234567"; formulario = cliente; }
        using var respuesta = await EnviarAsync(HttpMethod.Post, vehiculos ? "api/vehiculos" : "api/clientes", formulario);
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        using var json = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync());
        Assert.Equal(vehiculos ? "Esta placa ya está registrada." : "Ya existe un cliente registrado con este CI y complemento.",
            json.RootElement.GetProperty("errores").GetProperty(vehiculos ? "Placa" : "Ci").GetString());
        Assert.Equal(0, Puertos.Escrituras);
    }

    [Fact]
    public async Task Vehiculo_ClienteInexistenteYModeloInvalidoNoEscriben()
    {
        var formulario = VehiculoFormulario(); formulario.ClienteId = 99; formulario.Modelo = "Modelo inventado";
        using var respuesta = await EnviarAsync(HttpMethod.Post, "api/vehiculos", formulario);
        var resultado = (await respuesta.Content.ReadFromJsonAsync<ResultadoFormulario<VehiculoFormViewModel>>())!;
        Assert.Equal("El cliente seleccionado no existe.", resultado.Errores[nameof(VehiculoFormViewModel.ClienteId)]);
        Assert.Equal("Selecciona un modelo correspondiente a la marca.", resultado.Errores[nameof(VehiculoFormViewModel.Modelo)]);
        Assert.Equal(0, Puertos.Escrituras);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EdicionInexistente_ConservaErrorGeneralEnResultadoFormulario(bool vehiculos)
    {
        using var respuesta = await EnviarAsync(HttpMethod.Put, vehiculos ? "api/vehiculos/99" : "api/clientes/99",
            vehiculos ? VehiculoFormulario() : ClienteFormulario());
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        using var json = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync());
        Assert.Equal(99, json.RootElement.GetProperty("formulario").GetProperty("id").GetInt32());
        Assert.Equal(vehiculos ? "El vehículo que intentas editar ya no existe." : "El cliente solicitado no existe.",
            json.RootElement.GetProperty("errores").GetProperty("").GetString());
        Assert.Equal(0, Puertos.Escrituras);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ConflictoDuranteEscritura_SeTraducePorAplicacionSinFiltrarExcepciones(bool vehiculos)
    {
        Puertos.ErrorEscritura = new RegistroDuplicadoException(new InvalidOperationException());
        using var respuesta = await EnviarAsync(HttpMethod.Post, vehiculos ? "api/vehiculos" : "api/clientes",
            vehiculos ? VehiculoFormulario() : ClienteFormulario());
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        using var json = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync());
        Assert.True(json.RootElement.GetProperty("errores").TryGetProperty(vehiculos ? "Placa" : "Ci", out _));
        Assert.Equal(1, Puertos.Escrituras);
    }

    [Fact]
    public async Task ReferenciaPerdidaDuranteEscritura_ConservaErrorDeCliente()
    {
        Puertos.ErrorEscritura = new ReferenciaInexistenteException(new InvalidOperationException());
        using var respuesta = await EnviarAsync(HttpMethod.Post, "api/vehiculos", VehiculoFormulario());
        var resultado = (await respuesta.Content.ReadFromJsonAsync<ResultadoFormulario<VehiculoFormViewModel>>())!;
        Assert.Equal("El cliente seleccionado no existe.", resultado.Errores[nameof(VehiculoFormViewModel.ClienteId)]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EliminarRelacionado_DevuelveErrorApiSinBorrar(bool vehiculos)
    {
        if (vehiculos) Puertos.VehiculoConOrdenes = true;
        using var respuesta = await EnviarAsync(HttpMethod.Delete, vehiculos ? "api/vehiculos/7" : "api/clientes/7");
        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.Equal(vehiculos ? "No se puede eliminar el vehículo porque tiene órdenes asociadas." : "No se puede eliminar el cliente porque tiene vehículos asociados.",
            (await respuesta.Content.ReadFromJsonAsync<ErrorApi>())!.Mensaje);
        Assert.Contains(Puertos.Clientes, cliente => cliente.Id == 7);
        Assert.Contains(Puertos.Vehiculos, vehiculo => vehiculo.Id == 7);
        Assert.Empty(Puertos.Auditoria);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EliminarSinRelaciones_Devuelve204YAuditaUsuarioDeCabeceras(bool vehiculos)
    {
        if (!vehiculos) Puertos.Vehiculos.Clear();
        using var respuesta = await EnviarAsync(HttpMethod.Delete, vehiculos ? "api/vehiculos/7" : "api/clientes/7");
        Assert.Equal(HttpStatusCode.NoContent, respuesta.StatusCode);
        var auditoria = Assert.Single(Puertos.Auditoria);
        Assert.Equal(17, auditoria.UsuarioId);
        Assert.Equal("recepción_prueba", auditoria.Username);
        Assert.Equal(vehiculos ? "VEHICULO_ELIMINADO" : "CLIENTE_ELIMINADO", auditoria.Accion);
        Assert.Equal(1, Puertos.Escrituras);
        if (vehiculos) Assert.Empty(Puertos.Vehiculos);
        else Assert.DoesNotContain(Puertos.Clientes, cliente => cliente.Id == 7);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EliminarInexistente_ConservaMensajeEsperadoPorFrontend(bool vehiculos)
    {
        using var respuesta = await EnviarAsync(HttpMethod.Delete, vehiculos ? "api/vehiculos/99" : "api/clientes/99");
        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.Equal(vehiculos ? "El vehículo seleccionado ya no existe." : "El cliente solicitado no existe.",
            (await respuesta.Content.ReadFromJsonAsync<ErrorApi>())!.Mensaje);
        Assert.Equal(0, Puertos.Escrituras);
    }

    [Fact]
    public async Task Catalogo_EntregaMarcasYModelosRealesSinConsultarPersistencia()
    {
        using var respuesta = await EnviarAsync(HttpMethod.Get, "api/vehiculos/catalogo");
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var catalogo = (await respuesta.Content.ReadFromJsonAsync<IReadOnlyDictionary<string, IReadOnlyList<string>>>())!;
        Assert.Equal(CatalogoVehiculos.MarcasOrdenadas, catalogo.Keys);
        foreach (var marca in CatalogoVehiculos.MarcasOrdenadas)
            Assert.Equal(CatalogoVehiculos.ObtenerModelosOrdenados(marca), catalogo[marca]);
        Assert.Contains("Corolla", catalogo["Toyota"]);
        Assert.Equal(0, Puertos.Consultas);
    }

    [Theory]
    [InlineData("Administrador", true)]
    [InlineData("Recepcionista", true)]
    [InlineData("Invitado", false)]
    [InlineData("", false)]
    public async Task TodosLosEndpoints_RespetanRolesExistentes(string rol, bool autorizado)
    {
        foreach (var (metodo, ruta, formulario) in Endpoints())
        {
            using var respuesta = await EnviarAsync(metodo, ruta, formulario, rol);
            if (autorizado)
                Assert.True(respuesta.IsSuccessStatusCode || respuesta.StatusCode == HttpStatusCode.BadRequest,
                    $"{metodo} {ruta}: {(int)respuesta.StatusCode}");
            else
                Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
        }
        if (!autorizado) { Assert.Equal(0, Puertos.Consultas); Assert.Equal(0, Puertos.Escrituras); }
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
        Assert.Equal(0, Puertos.Consultas);
        Assert.Equal(0, Puertos.Escrituras);
    }

    [Theory]
    [InlineData("api/clientes", "null")]
    [InlineData("api/clientes", "{malformado")]
    [InlineData("api/vehiculos", "null")]
    [InlineData("api/vehiculos", "{\"kilometraje\":\"no numérico\"}")]
    public async Task JsonInvalido_Devuelve400SinEscribir(string ruta, string json)
    {
        using var solicitud = Solicitud(HttpMethod.Post, ruta);
        solicitud.Content = new StringContent(json, Encoding.UTF8, "application/json");
        using var respuesta = await _api.Http.SendAsync(solicitud);
        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.Equal(0, Puertos.Escrituras);
    }

    [Theory]
    [InlineData("api/clientes")]
    [InlineData("api/vehiculos")]
    public async Task FalloDePersistencia_UsaFiltroRealYNoExponeDetalles(string ruta)
    {
        Puertos.ErrorLectura = new BaseDatosApiException();
        using var respuesta = await EnviarAsync(HttpMethod.Get, ruta);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, respuesta.StatusCode);
        Assert.Equal("No se pudo acceder a los datos. Intenta nuevamente.", (await respuesta.Content.ReadFromJsonAsync<ErrorApi>())!.Mensaje);
        Assert.DoesNotContain("Detalle de persistencia", await respuesta.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RechazoDeAplicacion_UsaFormatoDeErrorApi(bool vehiculos)
    {
        Puertos.ErrorEscritura = new UnauthorizedAccessException("Se requiere un usuario autenticado.");
        using var respuesta = await EnviarAsync(HttpMethod.Post, vehiculos ? "api/vehiculos" : "api/clientes",
            vehiculos ? VehiculoFormulario() : ClienteFormulario());
        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
        Assert.Equal("Se requiere un usuario autenticado.", (await respuesta.Content.ReadFromJsonAsync<ErrorApi>())!.Mensaje);
    }

    private Task<HttpResponseMessage> EnviarAsync(HttpMethod metodo, string ruta, object? formulario = null,
        string rol = "Administrador", string? clave = ServiciosEnPrueba.ClaveInterna) => EnviarSolicitudAsync(metodo, ruta, formulario, rol, clave);

    private async Task<HttpResponseMessage> EnviarSolicitudAsync(HttpMethod metodo, string ruta, object? formulario,
        string rol, string? clave)
    {
        using var solicitud = Solicitud(metodo, ruta, rol, clave);
        if (formulario is not null) solicitud.Content = JsonContent.Create(formulario, formulario.GetType());
        return await _api.Http.SendAsync(solicitud);
    }

    private static HttpRequestMessage Solicitud(HttpMethod metodo, string ruta, string rol = "Administrador", string? clave = ServiciosEnPrueba.ClaveInterna)
    {
        var solicitud = new HttpRequestMessage(metodo, ruta);
        if (clave is not null) solicitud.Headers.Add("X-Clave-Interna", clave);
        solicitud.Headers.Add("X-Usuario-Id", "17");
        solicitud.Headers.Add("X-Usuario-Nombre", Uri.EscapeDataString("recepción_prueba"));
        if (rol != "") solicitud.Headers.Add("X-Usuario-Rol", rol);
        return solicitud;
    }

    private static ClienteFormViewModel ClienteFormulario() => new()
    { Ci = "3456789", ComplementoCi = "1a", Nombres = " mARía    fernANDA ", PrimerApellido = " pÉrez ", SegundoApellido = " rOjAs ", Celular = " 71234567 " };
    private static VehiculoFormViewModel VehiculoFormulario() => new()
    { Placa = " 456 def ", Marca = " toyota ", Modelo = " corolla ", ClienteId = 7, Kilometraje = 1500, Observaciones = "Revisión" };

    private static IEnumerable<(HttpMethod Metodo, string Ruta, object? Formulario)> Endpoints()
    {
        foreach (string ruta in new[] { "api/clientes", "api/vehiculos" })
        {
            object formulario = ruta == "api/clientes" ? ClienteFormulario() : VehiculoFormulario();
            yield return (HttpMethod.Get, ruta, null);
            yield return (HttpMethod.Get, ruta + "/sugerencias?termino=pr", null);
            yield return (HttpMethod.Get, ruta + "/7", null);
            yield return (HttpMethod.Post, ruta, formulario);
            yield return (HttpMethod.Put, ruta + "/7", formulario);
            yield return (HttpMethod.Delete, ruta + "/7", null);
        }
        yield return (HttpMethod.Get, "api/vehiculos/catalogo", null);
    }
}
