using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using TallerMecanico.Contratos;
using TallerMecanico.Dominio.Entidades;

namespace TallerMecanico.IntegrationTests;

[Trait("Area", "Backend")]
public sealed class ServiciosApiTests : IClassFixture<ServiciosApiFixture>
{
    private readonly ServiciosApiFixture _api;
    private PuertosServiciosApi Puertos => _api.Puertos;

    public ServiciosApiTests(ServiciosApiFixture api)
    {
        _api = api;
        Puertos.Reiniciar();
    }

    [Fact]
    public async Task ListadoBusquedaYSeleccion_ConservanRutasParametrosYDtoDeS13()
    {
        using var listado = await EnviarAsync(HttpMethod.Get, "api/servicios");
        Assert.Equal(HttpStatusCode.OK, listado.StatusCode);
        Assert.Equal(Puertos.Servicios.Count, (await listado.Content.ReadFromJsonAsync<ServicioDto[]>())!.Length);
        using var busqueda = await EnviarAsync(HttpMethod.Get, "api/servicios?terminoBusqueda=%20Revisi%C3%B3n%20");
        Assert.Equal("Revisión", Puertos.TerminoBusqueda);
        Assert.Equal(7, Assert.Single((await busqueda.Content.ReadFromJsonAsync<ServicioDto[]>())!).Id);
        using var vacio = await EnviarAsync(HttpMethod.Get, "api/servicios?terminoBusqueda=%20%20");
        Assert.Equal(Puertos.Servicios.Count, (await vacio.Content.ReadFromJsonAsync<ServicioDto[]>())!.Length);
        using var seleccionado = await EnviarAsync(HttpMethod.Get, "api/servicios/7");
        var servicio = (await seleccionado.Content.ReadFromJsonAsync<ServicioDto>())!;
        Assert.Equal("Servicio histórico", servicio.Nombre);
        Assert.Equal("Revisión previa", servicio.Descripcion);
        Assert.Equal(55.50m, servicio.Costo);
        Assert.Equal(1.75m, servicio.TiempoEstimadoHoras);
        Assert.Equal("llanta", servicio.UnidadCantidad);
        using var inexistente = await EnviarAsync(HttpMethod.Get, "api/servicios/9999");
        Assert.Equal(HttpStatusCode.NotFound, inexistente.StatusCode);
        Assert.Equal(0, Puertos.Escrituras);
    }

    [Fact]
    public async Task Sugerencias_DeleganNormalizacionSinRestringirLaBusquedaAdministrativaAlCatalogo()
    {
        using var respuesta = await EnviarAsync(HttpMethod.Get, "api/servicios/sugerencias?termino=%20cambio%20%20aceite%20");
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var sugerencia = Assert.Single((await respuesta.Content.ReadFromJsonAsync<SugerenciaServicio[]>())!);
        Assert.Equal(7, sugerencia.Id);
        Assert.Equal("Servicio histórico", sugerencia.TextoPrincipal);
        Assert.Equal("Revisión previa", sugerencia.TextoSecundario);
        Assert.Equal("cambio aceite", Puertos.TerminoSugerencias);
        Assert.False(Puertos.SoloCatalogoEnSugerencias);
    }

    [Theory]
    [InlineData("")]
    [InlineData("a")]
    [InlineData("%20%20")]
    public async Task SugerenciasCortas_NoConsultanPersistencia(string termino)
    {
        using var respuesta = await EnviarAsync(HttpMethod.Get, $"api/servicios/sugerencias?termino={termino}");
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.Empty((await respuesta.Content.ReadFromJsonAsync<SugerenciaServicio[]>())!);
        Assert.Equal(0, Puertos.Consultas);
    }

    [Fact]
    public async Task Catalogo_ConservaDiezServiciosUnidadesYPrecioVigenteSinSustituirloPorPrecioInicial()
    {
        var filtros = Puertos.Servicios.Single(servicio => servicio.CodigoCatalogo == "CAMBIO_FILTROS" && servicio.UnidadCantidad == "unidad");
        filtros.Costo = 125.75m;
        using var respuesta = await EnviarAsync(HttpMethod.Get, "api/servicios/catalogo", rol: "Recepcionista");
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var catalogo = (await respuesta.Content.ReadFromJsonAsync<ServicioDto[]>())!;
        Assert.Equal(CatalogoServicios.Servicios.Count, catalogo.Length);
        Assert.DoesNotContain(catalogo, servicio => servicio.Id == 7 || servicio.Id == 250);
        foreach (var definicion in CatalogoServicios.Servicios)
        {
            var servicio = Assert.Single(catalogo, servicio => servicio.CodigoCatalogo == definicion.Codigo);
            Assert.Equal(definicion.Nombre, servicio.Nombre);
            Assert.Equal(definicion.UnidadCantidad, servicio.UnidadCantidad);
            Assert.Equal(definicion.Codigo == "CAMBIO_FILTROS" ? 125.75m : definicion.PrecioInicial, servicio.Costo);
        }
        Assert.Equal(0, Puertos.Escrituras);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Historial_DevuelveDtoConImportesYFechaOriginalesInclusoSinRegistros(bool vacio)
    {
        if (vacio) Puertos.Historial.Clear();
        using var respuesta = await EnviarAsync(HttpMethod.Get, "api/servicios/historial");
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var historial = (await respuesta.Content.ReadFromJsonAsync<HistorialCostoServicioDto[]>())!;
        if (vacio) Assert.Empty(historial);
        else
        {
            var cambio = Assert.Single(historial);
            Assert.Equal(7, cambio.ServicioId);
            Assert.Equal("Servicio histórico", cambio.NombreServicio);
            Assert.Equal(45m, cambio.CostoAnterior);
            Assert.Equal(55.50m, cambio.CostoNuevo);
            Assert.Equal(new DateTime(2026, 11, 1, 9, 30, 0), cambio.FechaCambio);
        }
        Assert.Equal(0, Puertos.Escrituras);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" unidad ")]
    public async Task Crear_ConservaFormularioPrecioYUnidadSinPermitirAsignarCodigoCatalogo(string? unidad)
    {
        var formulario = Formulario(); formulario.UnidadCantidad = unidad;
        using var solicitud = Solicitud(HttpMethod.Post, "api/servicios");
        var cuerpo = JsonSerializer.SerializeToNode(formulario, new JsonSerializerOptions(JsonSerializerDefaults.Web))!.AsObject();
        cuerpo["id"] = 9999; cuerpo["codigoCatalogo"] = "CAMBIO_ACEITE"; cuerpo["creadoPor"] = "usuario_falso";
        solicitud.Content = JsonContent.Create(cuerpo);
        using var respuesta = await _api.Http.SendAsync(solicitud);
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var resultado = (await respuesta.Content.ReadFromJsonAsync<ResultadoFormulario<ServicioFormViewModel>>())!;
        Assert.Empty(resultado.Errores);
        Assert.Equal(formulario.Nombre, resultado.Formulario.Nombre);
        Assert.Equal(formulario.Costo, resultado.Formulario.Costo);
        Assert.Equal(formulario.TiempoEstimadoHoras, resultado.Formulario.TiempoEstimadoHoras);
        Assert.Equal(string.IsNullOrWhiteSpace(unidad) ? null : "unidad", resultado.Formulario.UnidadCantidad);
        var creado = Assert.Single(Puertos.Servicios, servicio => servicio.Nombre == formulario.Nombre);
        Assert.NotEqual(9999, creado.Id);
        Assert.Null(creado.CodigoCatalogo);
        Assert.Equal("recepción_prueba", creado.CreadoPor);
        Assert.NotEqual(default, creado.FechaCreacion);
        Assert.Equal(1, Puertos.Escrituras);
        Assert.Single(Puertos.Historial);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DataAnnotations_DevuelvenErroresPorCampoCon200SinPerderDatos(bool actualizar)
    {
        var formulario = new ServicioFormViewModel();
        using var respuesta = await GuardarAsync(actualizar, formulario);
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var resultado = (await respuesta.Content.ReadFromJsonAsync<ResultadoFormulario<ServicioFormViewModel>>())!;
        Assert.Equal("El nombre del servicio es obligatorio.", resultado.Errores[nameof(ServicioFormViewModel.Nombre)]);
        Assert.Equal("La descripción del servicio es obligatoria.", resultado.Errores[nameof(ServicioFormViewModel.Descripcion)]);
        Assert.Equal("El costo debe ser mayor a cero.", resultado.Errores[nameof(ServicioFormViewModel.Costo)]);
        Assert.Equal("El tiempo estimado debe ser mayor a cero.", resultado.Errores[nameof(ServicioFormViewModel.TiempoEstimadoHoras)]);
        Assert.Equal(4, resultado.Errores.Count);
        Assert.Equal(0, resultado.Formulario.Costo);
        Assert.Equal(0, Puertos.Escrituras);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ValidacionesCombinadas_ConservanReglasYUnSoloErrorPorCampo(bool actualizar)
    {
        var formulario = Formulario();
        formulario.Nombre = "Servicio  doble"; formulario.Descripcion = new string('x', 151);
        formulario.Costo = -1; formulario.UnidadCantidad = new string('u', 21);
        using var respuesta = await GuardarAsync(actualizar, formulario);
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var resultado = (await respuesta.Content.ReadFromJsonAsync<ResultadoFormulario<ServicioFormViewModel>>())!;
        Assert.Equal("El nombre solo puede contener un espacio entre palabras.", resultado.Errores[nameof(ServicioFormViewModel.Nombre)]);
        Assert.Equal("La descripción no puede superar los 150 caracteres.", resultado.Errores[nameof(ServicioFormViewModel.Descripcion)]);
        Assert.Equal("El costo debe ser mayor a cero.", resultado.Errores[nameof(ServicioFormViewModel.Costo)]);
        Assert.Equal("La unidad no puede superar 20 caracteres.", resultado.Errores[nameof(ServicioFormViewModel.UnidadCantidad)]);
        Assert.Equal(4, resultado.Errores.Count);
        Assert.Equal(formulario.Nombre, resultado.Formulario.Nombre);
        Assert.Equal(formulario.Descripcion, resultado.Formulario.Descripcion);
        Assert.Equal(0, Puertos.Escrituras);
    }

    [Theory]
    [InlineData(" Servicio", "El nombre no puede comenzar ni terminar con espacios.")]
    [InlineData("Servicio@", "El nombre contiene caracteres no permitidos.")]
    public async Task ValidacionExistenteDeFormato_NoSeNormalizaNiSeOmite(string nombre, string mensaje)
    {
        var formulario = Formulario(); formulario.Nombre = nombre;
        using var respuesta = await GuardarAsync(false, formulario);
        var resultado = (await respuesta.Content.ReadFromJsonAsync<ResultadoFormulario<ServicioFormViewModel>>())!;
        Assert.Equal(mensaje, Assert.Single(resultado.Errores).Value);
        Assert.Equal(nombre, resultado.Formulario.Nombre);
        Assert.Equal(0, Puertos.Escrituras);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UnidadDesconocida_ConservaRechazoDeAplicacionComoErrorApi(bool actualizar)
    {
        var formulario = Formulario(); formulario.UnidadCantidad = "eje";
        using var respuesta = await GuardarAsync(actualizar, formulario);
        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.Equal("Selecciona una unidad de cobro válida.", (await respuesta.Content.ReadFromJsonAsync<ErrorApi>())!.Mensaje);
        Assert.Equal(0, Puertos.Escrituras);
    }

    [Fact]
    public async Task Actualizar_ConservaIdDeRutaAuditoriaYLecturaDeHistorialSimulado()
    {
        var formulario = Formulario(); formulario.UnidadCantidad = "llanta";
        using var respuesta = await GuardarAsync(true, formulario);
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.Empty((await respuesta.Content.ReadFromJsonAsync<ResultadoFormulario<ServicioFormViewModel>>())!.Errores);
        var servicio = Puertos.Servicios.Single(servicio => servicio.Id == 7);
        Assert.Equal(formulario.Nombre, servicio.Nombre);
        Assert.Equal(75.25m, servicio.Costo);
        Assert.Equal("autor_original", servicio.CreadoPor);
        Assert.Equal(new DateTime(2026, 11, 1), servicio.FechaCreacion);
        Assert.Equal("recepción_prueba", servicio.ModificadoPor);
        Assert.NotNull(servicio.FechaModificacion);
        using var historial = await EnviarAsync(HttpMethod.Get, "api/servicios/historial");
        var cambios = (await historial.Content.ReadFromJsonAsync<HistorialCostoServicioDto[]>())!;
        Assert.Equal(2, cambios.Length);
        Assert.Equal(55.50m, cambios[0].CostoAnterior);
        Assert.Equal(75.25m, cambios[0].CostoNuevo);
        Assert.Equal(45m, cambios[1].CostoAnterior);
        Assert.Equal(55.50m, cambios[1].CostoNuevo);
        Assert.Equal(1, Puertos.Escrituras);
    }

    [Fact]
    public async Task ActualizarSinCambiarCosto_NoSolicitaNuevoRegistroDeHistorial()
    {
        var formulario = Formulario(); formulario.Costo = 55.50m;
        using var respuesta = await GuardarAsync(true, formulario);
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.Single(Puertos.Historial);
    }

    [Fact]
    public async Task ActualizarCatalogo_ConservaCodigoUnidadYPrecioVigente()
    {
        var filtros = Puertos.Servicios.Single(servicio => servicio.CodigoCatalogo == "CAMBIO_FILTROS" && servicio.UnidadCantidad == "unidad");
        var formulario = Formulario(); formulario.UnidadCantidad = "unidad"; formulario.Costo = 145.75m;
        using var respuesta = await EnviarAsync(HttpMethod.Put, $"api/servicios/{filtros.Id}", formulario);
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var actualizado = Puertos.Servicios.Single(servicio => servicio.Id == filtros.Id);
        Assert.Equal("CAMBIO_FILTROS", actualizado.CodigoCatalogo);
        Assert.Equal("unidad", actualizado.UnidadCantidad);
        Assert.Equal(145.75m, actualizado.Costo);
    }

    [Fact]
    public async Task CambiarUnidadDeCatalogo_ConservaConflictoEsperadoPorS13()
    {
        var filtros = Puertos.Servicios.Single(servicio => servicio.CodigoCatalogo == "CAMBIO_FILTROS" && servicio.UnidadCantidad == "unidad");
        var formulario = Formulario(); formulario.UnidadCantidad = null;
        using var respuesta = await EnviarAsync(HttpMethod.Put, $"api/servicios/{filtros.Id}", formulario);
        Assert.Equal(HttpStatusCode.Conflict, respuesta.StatusCode);
        Assert.Equal("La unidad de cobro de un servicio del catálogo no se puede cambiar.", (await respuesta.Content.ReadFromJsonAsync<ErrorApi>())!.Mensaje);
        Assert.Equal("unidad", filtros.UnidadCantidad);
        Assert.Equal(0, Puertos.Escrituras);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ActualizarOEliminarInexistente_DevuelveMensajeCompatibleConFrontend(bool eliminar)
    {
        using var respuesta = await EnviarAsync(eliminar ? HttpMethod.Delete : HttpMethod.Put, "api/servicios/9999", eliminar ? null : Formulario());
        Assert.Equal(HttpStatusCode.Conflict, respuesta.StatusCode);
        Assert.Equal("El servicio solicitado no existe.", (await respuesta.Content.ReadFromJsonAsync<ErrorApi>())!.Mensaje);
        Assert.Equal(0, Puertos.Escrituras);
    }

    [Fact]
    public async Task EliminarCatalogo_ConservaRestriccionSinEscribir()
    {
        var servicio = Puertos.Servicios.Single(servicio => servicio.CodigoCatalogo == "CAMBIO_ACEITE");
        using var respuesta = await EnviarAsync(HttpMethod.Delete, $"api/servicios/{servicio.Id}");
        Assert.Equal(HttpStatusCode.Conflict, respuesta.StatusCode);
        Assert.Equal("No se puede eliminar un servicio del catálogo de Nueva Orden.", (await respuesta.Content.ReadFromJsonAsync<ErrorApi>())!.Mensaje);
        Assert.Equal(0, Puertos.Escrituras);
        Assert.Empty(Puertos.Auditoria);
    }

    [Fact]
    public async Task EliminarServicioOrdinario_Devuelve204AuditaYConservaHistorial()
    {
        using var respuesta = await EnviarAsync(HttpMethod.Delete, "api/servicios/7");
        Assert.Equal(HttpStatusCode.NoContent, respuesta.StatusCode);
        Assert.DoesNotContain(Puertos.Servicios, servicio => servicio.Id == 7);
        var auditoria = Assert.Single(Puertos.Auditoria);
        Assert.Equal("SERVICIO_ELIMINADO", auditoria.Accion);
        Assert.Equal(17, auditoria.UsuarioId);
        Assert.Equal("recepción_prueba", auditoria.Username);
        Assert.Single(Puertos.Historial);
    }

    [Theory]
    [InlineData("Administrador")]
    [InlineData("Recepcionista")]
    [InlineData("Invitado")]
    [InlineData("")]
    public async Task TodosLosEndpoints_RespetanLecturaEscrituraEHistorialAdministrativo(string rol)
    {
        foreach (var (metodo, ruta, formulario, permiteRecepcionista) in Endpoints())
        {
            bool autorizado = rol == "Administrador" || rol == "Recepcionista" && permiteRecepcionista;
            int consultas = Puertos.Consultas, escrituras = Puertos.Escrituras;
            using var respuesta = await EnviarAsync(metodo, ruta, formulario, rol);
            if (autorizado) Assert.True(respuesta.IsSuccessStatusCode, $"{rol}: {metodo} {ruta}: {(int)respuesta.StatusCode}");
            else
            {
                Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
                Assert.Equal(consultas, Puertos.Consultas);
                Assert.Equal(escrituras, Puertos.Escrituras);
            }
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("clave-incorrecta")]
    public async Task ClaveInternaAusenteOIncorrecta_RechazaTodosLosEndpoints(string? clave)
    {
        foreach (var (metodo, ruta, formulario, _) in Endpoints())
        {
            using var respuesta = await EnviarAsync(metodo, ruta, formulario, clave: clave);
            Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
        }
        Assert.Equal(0, Puertos.Consultas);
        Assert.Equal(0, Puertos.Escrituras);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("{malformado")]
    [InlineData("{\"costo\":\"no numérico\"}")]
    public async Task JsonInvalido_DevuelveError400SinEscribir(string json)
    {
        using var solicitud = Solicitud(HttpMethod.Post, "api/servicios");
        solicitud.Content = new StringContent(json, Encoding.UTF8, "application/json");
        using var respuesta = await _api.Http.SendAsync(solicitud);
        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.False(string.IsNullOrWhiteSpace((await respuesta.Content.ReadFromJsonAsync<ErrorApi>())!.Mensaje));
        Assert.Equal(0, Puertos.Escrituras);
    }

    [Theory]
    [InlineData("api/servicios")]
    [InlineData("api/servicios/catalogo")]
    [InlineData("api/servicios/historial")]
    [InlineData("api/servicios/sugerencias?termino=prueba")]
    public async Task FalloDePersistencia_UsaFiltroExistenteSinExponerDetalles(string ruta)
    {
        Puertos.ErrorLectura = new BaseDatosApiException();
        using var respuesta = await EnviarAsync(HttpMethod.Get, ruta);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, respuesta.StatusCode);
        Assert.Equal("No se pudo acceder a los datos. Intenta nuevamente.", (await respuesta.Content.ReadFromJsonAsync<ErrorApi>())!.Mensaje);
        Assert.DoesNotContain("Detalle de persistencia", await respuesta.Content.ReadAsStringAsync());
    }

    private Task<HttpResponseMessage> GuardarAsync(bool actualizar, ServicioFormViewModel formulario) =>
        EnviarAsync(actualizar ? HttpMethod.Put : HttpMethod.Post, actualizar ? "api/servicios/7" : "api/servicios", formulario);

    private async Task<HttpResponseMessage> EnviarAsync(HttpMethod metodo, string ruta, object? formulario = null,
        string rol = "Administrador", string? clave = ServiciosEnPrueba.ClaveInterna)
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

    private static ServicioFormViewModel Formulario() => new()
    { Nombre = "Servicio nuevo", Descripcion = "Revisión completa", Costo = 75.25m, TiempoEstimadoHoras = 2.5m, UnidadCantidad = "unidad" };

    private static IEnumerable<(HttpMethod Metodo, string Ruta, object? Formulario, bool PermiteRecepcionista)> Endpoints()
    {
        yield return (HttpMethod.Get, "api/servicios", null, true);
        yield return (HttpMethod.Get, "api/servicios/sugerencias?termino=pr", null, true);
        yield return (HttpMethod.Get, "api/servicios/7", null, true);
        yield return (HttpMethod.Get, "api/servicios/catalogo", null, true);
        yield return (HttpMethod.Get, "api/servicios/historial", null, false);
        yield return (HttpMethod.Post, "api/servicios", Formulario(), false);
        yield return (HttpMethod.Put, "api/servicios/7", Formulario(), false);
        yield return (HttpMethod.Delete, "api/servicios/7", null, false);
    }
}
