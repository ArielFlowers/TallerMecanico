using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Data.Common;
using TallerMecanico.Application;
using TallerMecanico.Application.Ordenes;
using TallerMecanico.Application.Ports;
using TallerMecanico.Data.Factories;
using TallerMecanico.Infraestructura.Adapters.MySql;
using TallerMecanico.Models;
using TallerMecanico.Patterns.FactoryMethod;
using TallerMecanico.Services;
using OrdenesIndexModel = TallerMecanico.Pages.Ordenes.IndexModel;

namespace TallerMecanico.IntegrationTests;

public sealed class ConsultaOrdenesTests
{
    [Theory]
    [InlineData("Administrador")]
    [InlineData("Recepcionista")]
    public void Fachada_ConsultaConFiltrosParaLosRolesAutorizados(string rol)
    {
        var puerto = new ConsultasPrueba();
        var fachada = new ConsultaOrdenesFacade(
            new ConsultaOrdenesService(puerto, new UsuarioPrueba { Rol = rol }));
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
        var fachada = new ConsultaOrdenesFacade(new ConsultaOrdenesService(puerto, usuario));

        Assert.Throws<UnauthorizedAccessException>(() => fachada.Obtener());
        Assert.Throws<UnauthorizedAccessException>(() => fachada.ObtenerDetalle(1));
        Assert.Equal(0, puerto.Consultas);
        Assert.Equal(0, puerto.ConsultasDetalle);
    }

    [Fact]
    public void PageModel_ConsultaMedianteFachadaYRefrescaConLosMismosFiltros()
    {
        var puerto = new ConsultasPrueba();
        var fachada = new ConsultaOrdenesFacade(new ConsultaOrdenesService(puerto, new UsuarioPrueba()));
        var pagina = new OrdenesIndexModel(fachada, NullLogger<OrdenesIndexModel>.Instance)
        {
            Estado = EstadoOrden.Activa,
            Fecha = new DateOnly(2026, 10, 12)
        };

        Assert.IsType<PageResult>(pagina.OnGet());
        Assert.Same(puerto.Ordenes, pagina.Ordenes);
        Assert.IsType<PageResult>(pagina.OnGet());
        Assert.Equal(2, puerto.Consultas);
        Assert.Equal(pagina.Estado, puerto.Estado);
        Assert.Equal(pagina.Fecha, puerto.Fecha);
    }

    [Fact]
    public void PageModel_FiltrosInvalidosNoEjecutanConsultas()
    {
        var puerto = new ConsultasPrueba();
        var fachada = new ConsultaOrdenesFacade(new ConsultaOrdenesService(puerto, new UsuarioPrueba()));
        var pagina = new OrdenesIndexModel(fachada, NullLogger<OrdenesIndexModel>.Instance);
        pagina.ModelState.AddModelError(nameof(pagina.Estado), "Estado inválido.");

        Assert.IsType<PageResult>(pagina.OnGet());
        Assert.Empty(pagina.Ordenes);
        Assert.Equal(0, puerto.Consultas);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Detalle_IdInvalidoNoConsultaElPuerto(int id)
    {
        var puerto = new ConsultasPrueba();
        var fachada = new ConsultaOrdenesFacade(new ConsultaOrdenesService(puerto, new UsuarioPrueba()));
        var pagina = new OrdenesIndexModel(fachada, NullLogger<OrdenesIndexModel>.Instance);

        Assert.Null(fachada.ObtenerDetalle(id));
        Assert.IsType<BadRequestObjectResult>(pagina.OnGetDetalle(id));
        Assert.Equal(0, puerto.ConsultasDetalle);
    }

    [Fact]
    public void Detalle_OrdenInexistenteDevuelve404SinConsultarListado()
    {
        var puerto = new ConsultasPrueba();
        var fachada = new ConsultaOrdenesFacade(new ConsultaOrdenesService(puerto, new UsuarioPrueba()));
        var pagina = new OrdenesIndexModel(fachada, NullLogger<OrdenesIndexModel>.Instance);

        Assert.Null(fachada.ObtenerDetalle(999));
        Assert.IsType<NotFoundObjectResult>(pagina.OnGetDetalle(999));
        Assert.Equal(0, puerto.Consultas);
    }

    [Fact]
    public void Detalle_ErrorDePersistenciaDevuelve503SinExponerElErrorTecnico()
    {
        var puerto = new ConsultasPrueba { FallarDetalle = true };
        var fachada = new ConsultaOrdenesFacade(new ConsultaOrdenesService(puerto, new UsuarioPrueba()));
        var pagina = new OrdenesIndexModel(fachada, NullLogger<OrdenesIndexModel>.Instance);

        var resultado = Assert.IsType<ObjectResult>(pagina.OnGetDetalle(1));
        Assert.Equal(503, resultado.StatusCode);
        Assert.Equal("No se pudo consultar la orden. Intenta nuevamente.", resultado.Value);
    }

    [Fact]
    public void FactoryMethod_ResuelveElAdaptadorDeLecturaDentroDelScope()
    {
        var servicios = new ServiceCollection();
        servicios.AddScoped<DatabaseConnectionFactory, ConexionProhibida>();
        servicios.AddScoped<CreadorConsultaOrdenes>();
        servicios.AddScoped<IConsultaOrdenesPort>(provider =>
            provider.GetRequiredService<CreadorConsultaOrdenes>().CrearRepositorio());
        using var provider = servicios.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        using var scope = provider.CreateScope();
        using var otroScope = provider.CreateScope();

        var puerto = scope.ServiceProvider.GetRequiredService<IConsultaOrdenesPort>();
        Assert.IsType<ConsultaOrdenesRepository>(puerto);
        Assert.Same(puerto, scope.ServiceProvider.GetRequiredService<IConsultaOrdenesPort>());
        Assert.NotSame(puerto, otroScope.ServiceProvider.GetRequiredService<IConsultaOrdenesPort>());
    }

    private sealed class ConsultasPrueba : IConsultaOrdenesPort
    {
        public IReadOnlyList<OrdenResumen> Ordenes { get; } =
            [new(1, new DateTime(2026, 10, 12), "1234ABC", "Ana Arce Pérez", "Juan López Núñez", 31m, EstadoOrden.Activa, "recepcion")];
        public int Consultas { get; private set; }
        public EstadoOrden? Estado { get; private set; }
        public DateOnly? Fecha { get; private set; }
        public OrdenDetalle Detalle { get; } = new() { Id = 1 };
        public int ConsultasDetalle { get; private set; }
        public bool FallarDetalle { get; init; }

        public OrdenDetalle? ObtenerDetalle(int id)
        {
            ConsultasDetalle++;
            if (FallarDetalle) throw new FalloConsultaPrueba();
            return id == Detalle.Id ? Detalle : null;
        }

        public IReadOnlyList<OrdenResumen> Obtener(EstadoOrden? estado = null, DateOnly? fecha = null)
        {
            Consultas++;
            Estado = estado;
            Fecha = fecha;
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
