using System.Data.Common;
using System.Net;
using TallerMecanico.Api;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging.Abstractions;
using TallerMecanico.Aplicacion.CasosUso.Ordenes;
using TallerMecanico.Aplicacion.CasosUso.Ordenes.Contratos;
using TallerMecanico.Dominio.Entidades;
using TallerMecanico.Pages.Ordenes;
using TallerMecanico.Aplicacion.Servicios;

namespace TallerMecanico.IntegrationTests;

[Trait("Area", "Backend")]
public sealed class ComprobanteOrdenTests
{
    [Theory]
    [InlineData("Administrador")]
    [InlineData("Recepcionista")]
    public void Fachada_GeneraParaRolesAutorizadosConIdentidadYFechaDelServidor(string rol)
    {
        var usuario = new UsuarioPrueba { Username = "generador_actual", Rol = rol };
        var consultas = new LecturaPrueba();
        var generador = new GeneradorPrueba();
        var reloj = new RelojPrueba();
        var fachada = SoporteComprobantePdf.CrearFachada(new ConsultarOrdenesCasoUso(consultas, usuario), usuario, generador, reloj);

        Assert.Same(generador.Pdf, fachada.GenerarComprobante(15));
        Assert.Same(consultas.Orden, generador.Orden);
        Assert.Equal("Son Treinta y Un Bolivianos con 50/100.", generador.Literal);
        Assert.Equal(usuario.Username, generador.Usuario);
        Assert.Equal(reloj.Ahora, generador.Fecha);
        usuario.Username = "otro_generador";
        reloj.Ahora += TimeSpan.FromMinutes(1);
        fachada.GenerarComprobante(15);
        Assert.Equal("otro_generador", generador.Usuario);
        Assert.Equal(reloj.Ahora, generador.Fecha);
        Assert.Equal("creador_original", consultas.Orden.CreadoPor);
        Assert.Equal(2, consultas.Consultas);
    }

    [Theory]
    [InlineData(false, "Administrador")]
    [InlineData(true, "Invitado")]
    public void Seguridad_NoConsultaNiGeneraSinPermiso(bool autenticado, string rol)
    {
        var usuario = new UsuarioPrueba { EstaAutenticado = autenticado, Rol = rol };
        var consultas = new LecturaPrueba();
        var generador = new GeneradorPrueba();
        var fachada = SoporteComprobantePdf.CrearFachada(new ConsultarOrdenesCasoUso(consultas, usuario), usuario, generador);
        Assert.Throws<UnauthorizedAccessException>(() => fachada.GenerarComprobante(15));
        Assert.Equal(0, consultas.Consultas);
        Assert.Null(generador.Orden);
    }

    [Theory]
    [InlineData(0, 400)]
    [InlineData(-1, 400)]
    [InlineData(999, 404)]
    public async Task Endpoint_IdInvalidoOInexistenteNoGeneraPdf(int id, int codigo)
    {
        var api = new ApiFalsa();
        var pagina = Pagina(api);
        Assert.Equal(codigo, Assert.IsAssignableFrom<ObjectResult>(await pagina.OnGetAsync(id)).StatusCode);
        if (id <= 0) Assert.Empty(api.Llamadas);
    }

    [Fact]
    public async Task Endpoint_EntregaBytesPdfRecibidosDeApiConNombreInline()
    {
        var api = new ApiFalsa(); byte[] pdf = "%PDF-prueba"u8.ToArray();
        api.Archivo("GET api/ordenes/15/comprobante", pdf);
        var pagina = Pagina(api);
        var respuesta = Assert.IsType<FileContentResult>(await pagina.OnGetAsync(15));
        Assert.Equal("application/pdf", respuesta.ContentType);
        Assert.Equal(pdf, respuesta.FileContents);
        Assert.Contains("inline", pagina.Response.Headers.ContentDisposition.ToString());
        Assert.Contains("Comprobante-Orden-15.pdf", pagina.Response.Headers.ContentDisposition.ToString());
        Assert.Equal(["GET api/ordenes/15/comprobante"], api.Llamadas);
    }

    [Fact]
    public async Task Endpoint_ErrorApiDevuelve503SinDetallesTecnicos()
    {
        var api = new ApiFalsa();
        api.Error("GET api/ordenes/15/comprobante", HttpStatusCode.InternalServerError, "Error interno de prueba");
        var resultado = Assert.IsType<ObjectResult>(await Pagina(api).OnGetAsync(15));
        Assert.Equal(503, resultado.StatusCode);
        Assert.Equal("No se pudo consultar la orden. Intenta nuevamente.", resultado.Value);
    }

    [Fact]
    public async Task Endpoint_ErrorInesperadoDevuelve500SinDetallesTecnicos()
    {
        var resultado = Assert.IsType<ObjectResult>(await Pagina(new ErrorTransporte()).OnGetAsync(15));
        Assert.Equal(500, resultado.StatusCode);
        Assert.Equal("No se pudo generar el comprobante. Intenta nuevamente.", resultado.Value);
    }

    [Fact]
    public async Task Endpoint_ApiRechazaRolDevuelveForbid()
    {
        var api = new ApiFalsa(); api.Error("GET api/ordenes/15/comprobante", HttpStatusCode.Forbidden);
        Assert.IsType<ForbidResult>(await Pagina(api).OnGetAsync(15));
    }

    private static ComprobanteModel Pagina(HttpMessageHandler handler) =>
        new(new OrdenesApi(new HttpClient(handler) { BaseAddress = new Uri("http://ordenes.prueba/") }),
            NullLogger<ComprobanteModel>.Instance)
        { PageContext = new PageContext { HttpContext = new DefaultHttpContext() } };

    private sealed class ErrorTransporte : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Error interno de prueba");
    }

    private sealed class LecturaPrueba : IOrdenQueryRepository
    {
        public OrdenDetalle Orden { get; } = SoporteComprobantePdf.Orden();
        public int Consultas { get; private set; }
        public bool Fallar { get; init; }
        public IReadOnlyList<OrdenResumen> Obtener(EstadoOrden? estado = null, DateOnly? fecha = null,
            string? terminoBusqueda = null, int? ordenSeleccionadaId = null) => [];
        public Task<IReadOnlyList<SugerenciaOrden>> SugerirAsync(string termino, EstadoOrden? estado = null,
            DateOnly? fecha = null, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<SugerenciaOrden>>([]);
        public OrdenDetalle? ObtenerDetalle(int id)
        {
            Consultas++;
            if (Fallar) throw new ErrorConsulta();
            return id == Orden.Id ? Orden : null;
        }
    }

    private sealed class GeneradorPrueba : IComprobantePdf
    {
        public byte[] Pdf { get; } = "%PDF-prueba"u8.ToArray();
        public OrdenDetalle? Orden { get; private set; }
        public string? Literal { get; private set; }
        public string? Usuario { get; private set; }
        public DateTimeOffset Fecha { get; private set; }
        public bool Fallar { get; init; }
        public byte[] Generar(OrdenDetalle orden, string totalLiteral, string generadoPor, DateTimeOffset fechaGeneracion)
        {
            if (Fallar) throw new InvalidOperationException("Error interno de prueba");
            Orden = orden; Literal = totalLiteral; Usuario = generadoPor; Fecha = fechaGeneracion;
            return Pdf;
        }
    }

    private sealed class RelojPrueba : TimeProvider
    {
        public DateTimeOffset Ahora { get; set; } = new(2026, 10, 12, 20, 30, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Ahora;
    }

    private sealed class ErrorConsulta() : DbException("Error interno de prueba");
}
