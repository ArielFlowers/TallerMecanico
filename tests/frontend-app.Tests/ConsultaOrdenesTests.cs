using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Data.Common;
using TallerMecanico.Aplicacion.CasosUso.Ordenes;
using TallerMecanico.Aplicacion.CasosUso.Ordenes.Contratos;
using TallerMecanico.Infraestructura.Persistencia;
using TallerMecanico.Infraestructura.Persistencia.MySql;
using TallerMecanico.Infraestructura.Persistencia.Fabricas;
using TallerMecanico.Aplicacion.Servicios;
using TallerMecanico.Dominio.Entidades;

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

        public OrdenDetalle? ObtenerDetalle(int id)
        {
            ConsultasDetalle++;
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
