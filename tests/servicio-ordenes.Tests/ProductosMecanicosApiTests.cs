using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using TallerMecanico.Aplicacion.Errores;
using TallerMecanico.Contratos;

namespace TallerMecanico.IntegrationTests;

[Trait("Area", "Backend")]
public sealed class ProductosMecanicosApiTests : IClassFixture<ProductosMecanicosApiFixture>
{
    private readonly ProductosMecanicosApiFixture _api;
    private PuertosProductosMecanicosApi Puertos => _api.Puertos;

    public ProductosMecanicosApiTests(ProductosMecanicosApiFixture api)
    {
        _api = api;
        Puertos.Reiniciar();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ListadoYBusqueda_ConservanParametrosYDtoDeS12(bool mecanicos)
    {
        string ruta = mecanicos ? "api/mecanicos" : "api/productos";
        using var listado = await EnviarAsync(HttpMethod.Get, ruta);
        Assert.Equal(HttpStatusCode.OK, listado.StatusCode);
        using var json = JsonDocument.Parse(await listado.Content.ReadAsStringAsync());
        Assert.Equal(2, json.RootElement.GetArrayLength());
        Assert.False(json.RootElement[0].TryGetProperty("modificadoPor", out _));

        string termino = mecanicos ? "José" : " Filtro ";
        using var busqueda = await EnviarAsync(HttpMethod.Get, $"{ruta}?terminoBusqueda={Uri.EscapeDataString(termino)}");
        Assert.Equal(HttpStatusCode.OK, busqueda.StatusCode);
        if (mecanicos)
        {
            Assert.Equal("José", Puertos.BusquedaMecanicos);
            Assert.Equal("Motores", Assert.Single((await busqueda.Content.ReadFromJsonAsync<MecanicoDto[]>())!).Especialidad);
        }
        else
        {
            Assert.Equal("Filtro", Puertos.BusquedaProductos);
            Assert.Equal("FIL-001", Assert.Single((await busqueda.Content.ReadFromJsonAsync<ProductoDto[]>())!).Codigo);
        }
        using var vacio = await EnviarAsync(HttpMethod.Get, $"{ruta}?terminoBusqueda=%20%20");
        using var datos = JsonDocument.Parse(await vacio.Content.ReadAsStringAsync());
        Assert.Equal(2, datos.RootElement.GetArrayLength());
        Assert.Equal(0, Puertos.Escrituras);
        Assert.Equal(0, Puertos.MovimientosStock);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Sugerencias_ConservanStockEspecialidadYNormalizacion(bool mecanicos)
    {
        using var respuesta = await EnviarAsync(HttpMethod.Get,
            (mecanicos ? "api/mecanicos" : "api/productos") + "/sugerencias?termino=%20Texto%20%20Prueba%20");
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        if (mecanicos)
        {
            var sugerencia = Assert.Single((await respuesta.Content.ReadFromJsonAsync<SugerenciaMecanico[]>())!);
            Assert.Equal(7, sugerencia.Id);
            Assert.Contains("Motores", sugerencia.TextoSecundario);
            Assert.True(Puertos.IncluyeEspecialidad);
            Assert.Equal("Texto Prueba", Puertos.SugerenciasMecanicos);
        }
        else
        {
            var sugerencia = Assert.Single((await respuesta.Content.ReadFromJsonAsync<SugerenciaProducto[]>())!);
            Assert.Equal(10, sugerencia.Stock);
            Assert.Equal("FIL-001", sugerencia.TextoSecundario);
            Assert.Equal("Texto Prueba", Puertos.SugerenciasProductos);
        }
        Assert.Equal(0, Puertos.MovimientosStock);
    }

    [Theory]
    [InlineData("api/productos", "")]
    [InlineData("api/productos", "a")]
    [InlineData("api/mecanicos", "")]
    [InlineData("api/mecanicos", "a")]
    public async Task SugerenciasCortas_NoConsultanLosPuertos(string ruta, string termino)
    {
        using var respuesta = await EnviarAsync(HttpMethod.Get, $"{ruta}/sugerencias?termino={termino}");
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.Empty((await respuesta.Content.ReadFromJsonAsync<JsonElement[]>())!);
        Assert.Equal(0, Puertos.Consultas);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ObtenerPorId_UsaMapeosYDevuelve404SiFalta(bool mecanicos)
    {
        string ruta = mecanicos ? "api/mecanicos" : "api/productos";
        using var respuesta = await EnviarAsync(HttpMethod.Get, $"{ruta}/7");
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        if (mecanicos)
        {
            var mecanico = (await respuesta.Content.ReadFromJsonAsync<MecanicoDto>())!;
            Assert.Equal("1A", mecanico.ComplementoCi);
            Assert.Equal("José", mecanico.Nombres);
            Assert.Equal("", mecanico.SegundoApellido);
            Assert.Equal("Masculino", mecanico.Genero);
            Assert.Equal("71234567", mecanico.Celular);
        }
        else
        {
            var producto = (await respuesta.Content.ReadFromJsonAsync<ProductoDto>())!;
            Assert.Equal(25.50m, producto.Precio);
            Assert.Equal(10, producto.Stock);
            Assert.Equal(3, producto.StockMinimo);
            Assert.Equal("autor_original", producto.CreadoPor);
            Assert.Equal(new DateTime(2026, 11, 1), producto.FechaCreacion);
        }
        using var inexistente = await EnviarAsync(HttpMethod.Get, $"{ruta}/99");
        Assert.Equal(HttpStatusCode.NotFound, inexistente.StatusCode);
    }

    [Fact]
    public async Task CrearProducto_DevuelveFormularioNormalizadoYPermiteRecuperarIdComoS12()
    {
        var formulario = ProductoFormulario(); formulario.Id = 999;
        using var respuesta = await EnviarAsync(HttpMethod.Post, "api/productos", formulario);
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var resultado = (await respuesta.Content.ReadFromJsonAsync<ResultadoFormulario<ProductoFormViewModel>>())!;
        Assert.Empty(resultado.Errores);
        Assert.Equal("NUEVO-001", resultado.Formulario.Codigo);
        Assert.Equal("Bujía de iridio", resultado.Formulario.Nombre);
        Assert.Equal(0, resultado.Formulario.Id);
        using var consulta = await EnviarAsync(HttpMethod.Get, "api/productos?terminoBusqueda=NUEVO-001");
        var producto = Assert.Single((await consulta.Content.ReadFromJsonAsync<ProductoDto[]>())!);
        Assert.True(producto.Id > 0);
        Assert.NotEqual(999, producto.Id);
        Assert.Equal(85.25m, producto.Precio);
        Assert.Equal(12, producto.Stock);
        Assert.Equal(2, producto.StockMinimo);
        Assert.Equal("recepción_prueba", producto.CreadoPor);
        Assert.Equal(1, Puertos.Escrituras);
        Assert.Equal(0, Puertos.MovimientosStock);
    }

    [Fact]
    public async Task CrearMecanico_DevuelveDiccionarioYConservaNormalizacionYSegundoApellidoOpcional()
    {
        using var respuesta = await EnviarAsync(HttpMethod.Post, "api/mecanicos", MecanicoFormulario());
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.Empty((await respuesta.Content.ReadFromJsonAsync<IReadOnlyDictionary<string, string>>())!);
        var mecanico = Puertos.Mecanicos.Single(mecanico => mecanico.Ci == "3456789");
        Assert.Equal("2B", mecanico.ComplementoCi);
        Assert.Equal("Juan Carlos", mecanico.Nombres);
        Assert.Equal("Pérez", mecanico.PrimerApellido);
        Assert.Equal("", mecanico.SegundoApellido);
        Assert.Equal("51234567", mecanico.Celular);
        Assert.Equal("Masculino", mecanico.Genero);
        Assert.Equal("Motores", mecanico.Especialidad);
        Assert.Equal("recepción_prueba", mecanico.CreadoPor);
        Assert.NotEqual(default, mecanico.FechaCreacion);
        Assert.Equal(1, Puertos.Escrituras);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Actualizar_UsaIdDeRutaYConservaAuditoria(bool mecanicos)
    {
        if (mecanicos)
        {
            using var respuesta = await EnviarAsync(HttpMethod.Put, "api/mecanicos/7", MecanicoFormulario());
            Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
            var resultado = (await respuesta.Content.ReadFromJsonAsync<ResultadoMecanico>())!;
            Assert.True(resultado.Actualizado); Assert.Empty(resultado.Errores);
            var mecanico = Puertos.Mecanicos.Single(mecanico => mecanico.Id == 7);
            Assert.Equal("Juan Carlos", mecanico.Nombres);
            Assert.Equal("autor_original", mecanico.CreadoPor);
            Assert.Equal(new DateTime(2026, 11, 1), mecanico.FechaCreacion);
            Assert.Equal("recepción_prueba", mecanico.ModificadoPor);
            Assert.NotNull(mecanico.FechaModificacion);
            Assert.Equal("María", Puertos.Mecanicos.Single(mecanico => mecanico.Id == 8).Nombres);
        }
        else
        {
            var formulario = ProductoFormulario(); formulario.Id = 8;
            using var respuesta = await EnviarAsync(HttpMethod.Put, "api/productos/7", formulario);
            Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
            var resultado = (await respuesta.Content.ReadFromJsonAsync<ResultadoFormulario<ProductoFormViewModel>>())!;
            Assert.Equal(7, resultado.Formulario.Id); Assert.Empty(resultado.Errores);
            var producto = Puertos.Productos.Single(producto => producto.Id == 7);
            Assert.Equal("NUEVO-001", producto.Codigo);
            Assert.Equal("autor_original", producto.CreadoPor);
            Assert.Equal(new DateTime(2026, 11, 1), producto.FechaCreacion);
            Assert.Equal("recepción_prueba", producto.ModificadoPor);
            Assert.Equal(12, producto.Stock);
            Assert.Equal("BUJ-002", Puertos.Productos.Single(producto => producto.Id == 8).Codigo);
        }
        Assert.Equal(1, Puertos.Escrituras);
        Assert.Equal(0, Puertos.MovimientosStock);
    }

    [Fact]
    public async Task ProductoInvalido_DevuelveErroresPorCampoSinEscribir()
    {
        using var respuesta = await EnviarAsync(HttpMethod.Post, "api/productos", new ProductoFormViewModel { Precio = -1, Stock = -1, StockMinimo = -1 });
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var resultado = (await respuesta.Content.ReadFromJsonAsync<ResultadoFormulario<ProductoFormViewModel>>())!;
        Assert.Equal("El código es obligatorio.", resultado.Errores[nameof(ProductoFormViewModel.Codigo)]);
        Assert.Equal("El nombre es obligatorio.", resultado.Errores[nameof(ProductoFormViewModel.Nombre)]);
        Assert.Equal("El precio debe ser mayor a cero.", resultado.Errores[nameof(ProductoFormViewModel.Precio)]);
        Assert.Equal("El stock no puede ser negativo.", resultado.Errores[nameof(ProductoFormViewModel.Stock)]);
        Assert.Equal("El stock mínimo no puede ser negativo.", resultado.Errores[nameof(ProductoFormViewModel.StockMinimo)]);
        Assert.Equal(0, Puertos.Escrituras);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MecanicoInvalido_ConservaErroresDeCiComplementoNombresApellidoYCelular(bool actualizar)
    {
        var formulario = MecanicoFormulario();
        formulario.Ci = "123 4567"; formulario.ComplementoCi = "A1"; formulario.Nombres = "Juan123";
        formulario.PrimerApellido = ""; formulario.Celular = "7071 2345";
        using var respuesta = await EnviarAsync(actualizar ? HttpMethod.Put : HttpMethod.Post,
            actualizar ? "api/mecanicos/7" : "api/mecanicos", formulario);
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        IReadOnlyDictionary<string, string> errores;
        if (actualizar)
        {
            var resultado = (await respuesta.Content.ReadFromJsonAsync<ResultadoMecanico>())!;
            Assert.False(resultado.Actualizado); errores = resultado.Errores;
        }
        else errores = (await respuesta.Content.ReadFromJsonAsync<IReadOnlyDictionary<string, string>>())!;
        Assert.Equal("El CI no debe contener espacios.", errores[nameof(MecanicoInputModel.Ci)]);
        Assert.Contains(nameof(MecanicoInputModel.ComplementoCi), errores.Keys);
        Assert.Contains(nameof(MecanicoInputModel.Nombres), errores.Keys);
        Assert.Equal("El primer apellido es obligatorio.", errores[nameof(MecanicoInputModel.PrimerApellido)]);
        Assert.Equal("El celular no debe contener espacios.", errores[nameof(MecanicoInputModel.Celular)]);
        Assert.Equal(0, Puertos.Escrituras);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Duplicados_ConservanMensajeYClaveSinEscrituras(bool mecanicos, bool actualizar)
    {
        object formulario;
        if (mecanicos) { var mecanico = MecanicoFormulario(); mecanico.Ci = "1234567"; mecanico.ComplementoCi = "1a"; formulario = mecanico; }
        else { var producto = ProductoFormulario(); producto.Codigo = " fil-001 "; formulario = producto; }
        string ruta = mecanicos ? "api/mecanicos" : "api/productos";
        using var respuesta = await EnviarAsync(actualizar ? HttpMethod.Put : HttpMethod.Post, ruta + (actualizar ? "/8" : ""), formulario);
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        using var json = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync());
        var errores = mecanicos && !actualizar ? json.RootElement : json.RootElement.GetProperty("errores");
        Assert.Equal(mecanicos ? "Ya existe un mecánico registrado con este CI." : "Ya existe un producto registrado con este código.",
            errores.GetProperty(mecanicos ? "Ci" : "Codigo").GetString());
        Assert.Equal(0, Puertos.Escrituras);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ActualizarMismoCodigoOCi_NoConsideraDuplicadoAlPropioRegistro(bool mecanicos)
    {
        object formulario;
        if (mecanicos) { var mecanico = MecanicoFormulario(); mecanico.Ci = "1234567"; mecanico.ComplementoCi = "1A"; formulario = mecanico; }
        else { var producto = ProductoFormulario(); producto.Codigo = "FIL-001"; formulario = producto; }
        using var respuesta = await EnviarAsync(HttpMethod.Put, mecanicos ? "api/mecanicos/7" : "api/productos/7", formulario);
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        using var json = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync());
        Assert.Empty(json.RootElement.GetProperty("errores").EnumerateObject());
        Assert.Equal(1, Puertos.Escrituras);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ActualizarInexistente_ConservaResultadoEsperadoPorFrontend(bool mecanicos)
    {
        using var respuesta = await EnviarAsync(HttpMethod.Put, mecanicos ? "api/mecanicos/99" : "api/productos/99",
            mecanicos ? MecanicoFormulario() : ProductoFormulario());
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        if (mecanicos)
        {
            var resultado = (await respuesta.Content.ReadFromJsonAsync<ResultadoMecanico>())!;
            Assert.False(resultado.Actualizado); Assert.Empty(resultado.Errores);
        }
        else
        {
            var resultado = (await respuesta.Content.ReadFromJsonAsync<ResultadoFormulario<ProductoFormViewModel>>())!;
            Assert.Equal("El producto solicitado no existe.", resultado.Errores[string.Empty]);
            Assert.Equal(99, resultado.Formulario.Id);
        }
        Assert.Equal(0, Puertos.Escrituras);
    }

    [Fact]
    public async Task ProductoDuplicadoDuranteEscritura_UsaTraduccionExistenteDeAplicacion()
    {
        Puertos.ErrorEscritura = new RegistroDuplicadoException(new InvalidOperationException());
        using var respuesta = await EnviarAsync(HttpMethod.Post, "api/productos", ProductoFormulario());
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var resultado = (await respuesta.Content.ReadFromJsonAsync<ResultadoFormulario<ProductoFormViewModel>>())!;
        Assert.Equal("Ya existe un producto registrado con este código.", resultado.Errores[nameof(ProductoFormViewModel.Codigo)]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EliminarRelacionado_ConservaDatosYMensaje(bool mecanicos)
    {
        Puertos.TieneOrdenes = true;
        using var respuesta = await EnviarAsync(HttpMethod.Delete, mecanicos ? "api/mecanicos/7" : "api/productos/7");
        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.Equal(mecanicos ? "No se puede eliminar el mecánico porque tiene órdenes asociadas." : "No se puede eliminar el producto porque tiene órdenes asociadas.",
            (await respuesta.Content.ReadFromJsonAsync<ErrorApi>())!.Mensaje);
        Assert.Equal(2, Puertos.Productos.Count);
        Assert.Equal(2, Puertos.Mecanicos.Count);
        Assert.Empty(Puertos.Auditoria);
        Assert.Equal(0, Puertos.MovimientosStock);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Eliminar_Devuelve204YAuditaSinMovimientosDeInventario(bool mecanicos)
    {
        using var respuesta = await EnviarAsync(HttpMethod.Delete, mecanicos ? "api/mecanicos/7" : "api/productos/7");
        Assert.Equal(HttpStatusCode.NoContent, respuesta.StatusCode);
        var auditoria = Assert.Single(Puertos.Auditoria);
        Assert.Equal(mecanicos ? "MECANICO_ELIMINADO" : "PRODUCTO_ELIMINADO", auditoria.Accion);
        Assert.Equal(17, auditoria.UsuarioId);
        Assert.Equal("recepción_prueba", auditoria.Username);
        if (mecanicos) Assert.DoesNotContain(Puertos.Mecanicos, mecanico => mecanico.Id == 7);
        else Assert.DoesNotContain(Puertos.Productos, producto => producto.Id == 7);
        Assert.Equal(0, Puertos.MovimientosStock);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EliminarInexistente_DevuelveErrorApiEsperado(bool mecanicos)
    {
        using var respuesta = await EnviarAsync(HttpMethod.Delete, mecanicos ? "api/mecanicos/99" : "api/productos/99");
        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.Equal(mecanicos ? "El mecánico seleccionado ya no existe." : "El producto solicitado no existe.",
            (await respuesta.Content.ReadFromJsonAsync<ErrorApi>())!.Mensaje);
        Assert.Equal(0, Puertos.Escrituras);
    }

    [Theory]
    [InlineData("Administrador")]
    [InlineData("Recepcionista")]
    [InlineData("Invitado")]
    [InlineData("")]
    public async Task TodosLosEndpoints_RespetanPermisosDeLecturaYEscritura(string rol)
    {
        foreach (var (metodo, ruta, formulario, permiteRecepcionista) in Endpoints())
        {
            bool autorizado = rol == "Administrador" || rol == "Recepcionista" && permiteRecepcionista;
            int consultasAntes = Puertos.Consultas, escriturasAntes = Puertos.Escrituras;
            using var respuesta = await EnviarAsync(metodo, ruta, formulario, rol);
            if (autorizado)
                Assert.True(respuesta.IsSuccessStatusCode || respuesta.StatusCode == HttpStatusCode.BadRequest,
                    $"{rol}: {metodo} {ruta}: {(int)respuesta.StatusCode}");
            else
            {
                Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
                Assert.Equal(consultasAntes, Puertos.Consultas);
                Assert.Equal(escriturasAntes, Puertos.Escrituras);
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

    [Fact]
    public async Task Recepcionista_PuedeCrearProductoPeroNoAdministrarProductosNiEscribirMecanicos()
    {
        using var respuesta = await EnviarAsync(HttpMethod.Post, "api/productos", ProductoFormulario(), "Recepcionista");
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.Empty((await respuesta.Content.ReadFromJsonAsync<ResultadoFormulario<ProductoFormViewModel>>())!.Errores);
        Assert.Equal(1, Puertos.Escrituras);
        foreach (var (metodo, ruta, formulario) in new (HttpMethod, string, object?)[]
        {
            (HttpMethod.Put, "api/productos/7", ProductoFormulario()),
            (HttpMethod.Delete, "api/productos/7", null),
            (HttpMethod.Post, "api/mecanicos", MecanicoFormulario()),
            (HttpMethod.Put, "api/mecanicos/7", MecanicoFormulario()),
            (HttpMethod.Delete, "api/mecanicos/7", null)
        })
        {
            using var rechazo = await EnviarAsync(metodo, ruta, formulario, "Recepcionista");
            Assert.Equal(HttpStatusCode.Forbidden, rechazo.StatusCode);
        }
        Assert.Equal(1, Puertos.Escrituras);
    }

    [Theory]
    [InlineData("api/productos", "null")]
    [InlineData("api/productos", "{\"stock\":1.5}")]
    [InlineData("api/mecanicos", "null")]
    [InlineData("api/mecanicos", "{malformado")]
    public async Task JsonInvalido_Devuelve400SinEscribir(string ruta, string json)
    {
        using var solicitud = Solicitud(HttpMethod.Post, ruta);
        solicitud.Content = new StringContent(json, Encoding.UTF8, "application/json");
        using var respuesta = await _api.Http.SendAsync(solicitud);
        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.Equal(0, Puertos.Escrituras);
    }

    [Theory]
    [InlineData("api/productos")]
    [InlineData("api/mecanicos")]
    public async Task FalloDePersistencia_UsaFiltroRealSinExponerDetalles(string ruta)
    {
        Puertos.ErrorLectura = new BaseDatosApiException();
        using var respuesta = await EnviarAsync(HttpMethod.Get, ruta);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, respuesta.StatusCode);
        Assert.Equal("No se pudo acceder a los datos. Intenta nuevamente.", (await respuesta.Content.ReadFromJsonAsync<ErrorApi>())!.Mensaje);
        Assert.DoesNotContain("Detalle de persistencia", await respuesta.Content.ReadAsStringAsync());
    }

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

    private static ProductoFormViewModel ProductoFormulario() => new()
    { Codigo = " nuevo-001 ", Nombre = " Bujía   de iridio ", Precio = 85.25m, Stock = 12, StockMinimo = 2 };
    private static MecanicoInputModel MecanicoFormulario() => new()
    { Ci = " 3456789 ", ComplementoCi = "2b", Nombres = " jUAN    caRLOS ", PrimerApellido = " pÉrez ", SegundoApellido = null, Genero = " Masculino ", Especialidad = " Motores ", Celular = " 51234567 " };

    private static IEnumerable<(HttpMethod Metodo, string Ruta, object? Formulario, bool PermiteRecepcionista)> Endpoints()
    {
        foreach (bool mecanicos in new[] { false, true })
        {
            string ruta = mecanicos ? "api/mecanicos" : "api/productos";
            object formulario = mecanicos ? MecanicoFormulario() : ProductoFormulario();
            yield return (HttpMethod.Get, ruta, null, true);
            yield return (HttpMethod.Get, ruta + "/sugerencias?termino=pr", null, true);
            yield return (HttpMethod.Get, ruta + "/7", null, true);
            yield return (HttpMethod.Post, ruta, formulario, !mecanicos);
            yield return (HttpMethod.Put, ruta + "/7", formulario, false);
            yield return (HttpMethod.Delete, ruta + "/7", null, false);
        }
    }
}
