using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using TallerMecanico.Api;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Data.Common;
using TallerMecanico.Aplicacion.Fachadas;
using TallerMecanico.Aplicacion.CasosUso.Ordenes;
using TallerMecanico.Aplicacion.CasosUso.Ordenes.Contratos;
using TallerMecanico.Infraestructura.Persistencia;
using TallerMecanico.Infraestructura.Persistencia.MySql;
using TallerMecanico.Dominio.Entidades;
using TallerMecanico.Infraestructura.Persistencia.Fabricas;
using TallerMecanico.Aplicacion.Servicios;
using OrdenesIndexModel = TallerMecanico.Pages.Ordenes.IndexModel;

namespace TallerMecanico.IntegrationTests;

[Trait("Area", "Backend")]
public sealed class ConsultaOrdenesTests
{
    [Theory]
    [InlineData("Administrador")]
    [InlineData("Recepcionista")]
    public void Fachada_ConsultaConFiltrosParaLosRolesAutorizados(string rol)
    {
        var puerto = new ConsultasPrueba();
        var fachada = SoporteComprobantePdf.CrearFachada(
            new ConsultarOrdenesCasoUso(puerto, new UsuarioPrueba { Rol = rol }));
        var fecha = new DateOnly(2026, 10, 12);

        Assert.Same(puerto.Ordenes, fachada.Obtener(EstadoOrden.Anulada, fecha));
        Assert.Equal(EstadoOrden.Anulada, puerto.Estado);
        Assert.Equal(fecha, puerto.Fecha);
        Assert.Equal(1, puerto.Consultas);
        Assert.Same(puerto.Detalle, fachada.ObtenerDetalle(1));
        Assert.Equal(1, puerto.ConsultasDetalle);
    }

    [Theory]
    [InlineData(false, "Administrador", "admin")]
    [InlineData(true, "Invitado", "invitado")]
    [InlineData(true, "Administrador", "")]
    public void Seguridad_NoConsultaElPuertoSinAutorizacion(bool autenticado, string rol, string username)
    {
        var puerto = new ConsultasPrueba();
        var usuario = new UsuarioPrueba { EstaAutenticado = autenticado, Rol = rol, Username = username };
        var fachada = SoporteComprobantePdf.CrearFachada(new ConsultarOrdenesCasoUso(puerto, usuario));

        Assert.Throws<UnauthorizedAccessException>(() => fachada.Obtener());
        Assert.Throws<UnauthorizedAccessException>(() => fachada.ObtenerDetalle(1));
        Assert.Equal(0, puerto.Consultas);
        Assert.Equal(0, puerto.ConsultasDetalle);
    }

    [Fact]
    public async Task PageModel_ConsultaMedianteApiYRefrescaConLosMismosFiltros()
    {
        var puerto = new ConsultasPrueba();
        var api = new ApiFalsa();
        api.Json("GET api/ordenes", puerto.Ordenes);
        var pagina = Pagina(api);
        pagina.Estado = TallerMecanico.Contratos.Ordenes.EstadoOrden.Activa;
        pagina.Fecha = new DateOnly(2026, 10, 12);
        pagina.TerminoBusqueda = "1234ABC";
        pagina.OrdenSeleccionadaId = 1;

        Assert.IsType<PageResult>(await pagina.OnGetAsync());
        Assert.Equal(puerto.Ordenes.Select(orden => orden.Id), pagina.Ordenes.Select(orden => orden.Id));
        Assert.IsType<PageResult>(await pagina.OnGetAsync());
        Assert.Equal(2, api.Llamadas.Count);
        Assert.Equal(api.Llamadas[0], api.Llamadas[1]);
        Assert.Contains("estado=Activa", api.Llamadas[0]);
        Assert.Contains("fecha=2026-10-12", api.Llamadas[0]);
        Assert.Contains("terminoBusqueda=1234ABC", api.Llamadas[0]);
        Assert.Contains("ordenSeleccionadaId=1", api.Llamadas[0]);
    }

    [Fact]
    public async Task PageModel_FiltrosInvalidosNoEjecutanConsultas()
    {
        var api = new ApiFalsa();
        var pagina = Pagina(api);
        pagina.ModelState.AddModelError(nameof(pagina.Estado), "Estado inválido.");

        Assert.IsType<PageResult>(await pagina.OnGetAsync());
        Assert.Empty(pagina.Ordenes);
        Assert.Empty(api.Llamadas);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Detalle_IdInvalidoNoConsultaElPuerto(int id)
    {
        var puerto = new ConsultasPrueba();
        var fachada = SoporteComprobantePdf.CrearFachada(new ConsultarOrdenesCasoUso(puerto, new UsuarioPrueba()));
        var api = new ApiFalsa();
        Assert.Null(fachada.ObtenerDetalle(id));
        Assert.IsType<BadRequestObjectResult>(await Pagina(api).OnGetDetalleAsync(id));
        Assert.Empty(api.Llamadas);
        Assert.Equal(0, puerto.ConsultasDetalle);
    }

    [Fact]
    public async Task Detalle_OrdenInexistenteDevuelve404SinConsultarListado()
    {
        var api = new ApiFalsa();
        api.Error("GET api/ordenes/999", HttpStatusCode.NotFound);
        Assert.IsType<NotFoundObjectResult>(await Pagina(api).OnGetDetalleAsync(999));
        Assert.Equal(new[] { "GET api/ordenes/999" }, api.Llamadas);
    }

    [Fact]
    public async Task Detalle_ErrorDePersistenciaDevuelve503SinExponerElErrorTecnico()
    {
        var api = new ApiFalsa();
        api.Error("GET api/ordenes/1", HttpStatusCode.ServiceUnavailable, "Error técnico de prueba");
        var resultado = Assert.IsType<ObjectResult>(await Pagina(api).OnGetDetalleAsync(1));
        Assert.Equal(503, resultado.StatusCode);
        Assert.Equal("No se pudo consultar la orden. Intenta nuevamente.", resultado.Value);
    }

    private static OrdenesIndexModel Pagina(ApiFalsa api)
    {
        var contexto = new DefaultHttpContext();
        return new(new OrdenesApi(api.Cliente()), NullLogger<OrdenesIndexModel>.Instance)
        {
            PageContext = new PageContext { HttpContext = contexto },
            TempData = new TempDataDictionary(contexto, new TemporalesPrueba())
        };
    }

    [Fact]
    public void FactoryMethod_ResuelveElAdaptadorDeLecturaDentroDelScope()
    {
        var servicios = new ServiceCollection();
        servicios.AddScoped<DatabaseConnectionFactory, ConexionProhibida>();
        servicios.AddScoped<CreadorConsultaOrdenes>();
        servicios.AddScoped<IOrdenQueryRepository>(provider =>
            provider.GetRequiredService<CreadorConsultaOrdenes>().CrearRepositorio());
        using var provider = servicios.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        using var scope = provider.CreateScope();
        using var otroScope = provider.CreateScope();

        var puerto = scope.ServiceProvider.GetRequiredService<IOrdenQueryRepository>();
        Assert.IsType<ConsultaOrdenesRepository>(puerto);
        Assert.Same(puerto, scope.ServiceProvider.GetRequiredService<IOrdenQueryRepository>());
        Assert.NotSame(puerto, otroScope.ServiceProvider.GetRequiredService<IOrdenQueryRepository>());
    }

    private sealed class ConsultasPrueba : IOrdenQueryRepository
    {
        public IReadOnlyList<OrdenResumen> Ordenes { get; } =
            [new(1, new DateTime(2026, 10, 12), "1234ABC", "Ana Arce Pérez", "Juan López Núñez", "Cambio de aceite", 31m, EstadoOrden.Activa, "recepcion")];
        public int Consultas { get; private set; }
        public EstadoOrden? Estado { get; private set; }
        public DateOnly? Fecha { get; private set; }
        public string? TerminoBusqueda { get; private set; }
        public int? OrdenSeleccionadaId { get; private set; }
        public OrdenDetalle Detalle { get; } = new() { Id = 1 };
        public int ConsultasDetalle { get; private set; }
        public bool FallarDetalle { get; init; }

        public OrdenDetalle? ObtenerDetalle(int id)
        {
            ConsultasDetalle++;
            if (FallarDetalle) throw new FalloConsultaPrueba();
            return id == Detalle.Id ? Detalle : null;
        }

        public Task<IReadOnlyList<SugerenciaOrden>> SugerirAsync(string termino, EstadoOrden? estado = null,
            DateOnly? fecha = null, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<SugerenciaOrden>>([]);

        public IReadOnlyList<OrdenResumen> Obtener(EstadoOrden? estado = null, DateOnly? fecha = null,
            string? terminoBusqueda = null, int? ordenSeleccionadaId = null)
        {
            Consultas++;
            Estado = estado;
            Fecha = fecha;
            TerminoBusqueda = terminoBusqueda;
            OrdenSeleccionadaId = ordenSeleccionadaId;
            return Ordenes;
        }
    }

    private sealed class FalloConsultaPrueba() : DbException("Error técnico de prueba");

    private sealed class ConexionProhibida : DatabaseConnectionFactory
    {
        public ConexionProhibida() : base(
            new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Prueba"] = "sin conexión real"
            }).Build(), "Prueba") { }

        public override DbConnection CreateConnection() =>
            throw new InvalidOperationException("Resolver el puerto no debe abrir una conexión.");
    }
}
