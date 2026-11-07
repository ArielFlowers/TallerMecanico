using System.Net;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TallerMecanico.Aplicacion.Puertos;
using TallerMecanico.Aplicacion.Servicios;
using TallerMecanico.Aplicacion.Validaciones;
using TallerMecanico.Controllers;
using TallerMecanico.Dominio.Entidades;
using TallerMecanico.Infraestructura.Seguridad;

namespace TallerMecanico.IntegrationTests;

public sealed class ServiciosApiFixture : IAsyncLifetime
{
    private WebApplication? _app;
    internal PuertosServiciosApi Puertos { get; } = new();
    public HttpClient Http { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = "Testing",
            ContentRootPath = AppContext.BaseDirectory,
            ApplicationName = typeof(ServiciosController).Assembly.GetName().Name
        });
        builder.Configuration.Sources.Clear();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        { ["ClaveInterna"] = ServiciosEnPrueba.ClaveInterna });
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(opciones => opciones.Listen(IPAddress.Loopback, 0));
        builder.Services.AddControllers(opciones =>
        {
            opciones.Filters.Add<ErroresApiFilter>();
            opciones.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true;
        }).AddApplicationPart(typeof(ServiciosController).Assembly);
        builder.Services.AddAuthentication(ClaveInternaHandler.Esquema)
            .AddScheme<AuthenticationSchemeOptions, ClaveInternaHandler>(ClaveInternaHandler.Esquema, null);
        builder.Services.AddAuthorization(opciones =>
            opciones.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddScoped<ICurrentUser, CurrentUser>();
        builder.Services.AddSingleton<IRepository<Servicio>>(Puertos);
        builder.Services.AddSingleton<IConsultaServiciosPort>(Puertos);
        builder.Services.AddSingleton<IHistorialCostoServicioRepository>(Puertos);
        builder.Services.AddSingleton<IAuditoriaPort>(Puertos);
        builder.Services.AddScoped<ValidacionServicios>();
        builder.Services.AddScoped<IServicioService, ServicioService>();
        builder.Services.AddScoped<IHistorialCostoServicioService, HistorialCostoServicioService>();

        // MVC, filtros, autenticación y Aplicación reales, sin iniciar Program ni MySQL.
        _app = builder.Build();
        _app.UseAuthentication();
        _app.UseAuthorization();
        _app.MapControllers();
        try
        {
            await _app.StartAsync();
            string direccion = _app.Services.GetRequiredService<IServer>()
                .Features.Get<IServerAddressesFeature>()!.Addresses.Single();
            Http = new HttpClient { BaseAddress = new Uri(direccion), Timeout = TimeSpan.FromSeconds(10) };
        }
        catch
        {
            await _app.DisposeAsync();
            throw;
        }
    }

    public async Task DisposeAsync()
    {
        Http?.Dispose();
        if (_app is null) return;
        using var cancelacion = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        try { await _app.StopAsync(cancelacion.Token); }
        finally { await _app.DisposeAsync(); }
    }
}

internal sealed class PuertosServiciosApi : IRepository<Servicio>, IConsultaServiciosPort, IHistorialCostoServicioRepository, IAuditoriaPort
{
    public List<Servicio> Servicios { get; } = [];
    public List<HistorialCostoServicio> Historial { get; } = [];
    public List<RegistroAuditoria> Auditoria { get; } = [];
    public string? TerminoBusqueda { get; private set; }
    public string? TerminoSugerencias { get; private set; }
    public bool SoloCatalogoEnSugerencias { get; private set; }
    public int Consultas { get; private set; }
    public int Escrituras { get; private set; }
    public Exception? ErrorLectura { get; set; }
    public Exception? ErrorEscritura { get; set; }

    public void Reiniciar()
    {
        Servicios.Clear(); Historial.Clear(); Auditoria.Clear();
        Servicios.Add(new Servicio
        {
            Id = 7, Nombre = "Servicio histórico", Descripcion = "Revisión previa", Costo = 55.50m,
            TiempoEstimadoHoras = 1.75m, UnidadCantidad = "llanta", CreadoPor = "autor_original", FechaCreacion = new DateTime(2026, 11, 1)
        });
        foreach (var definicion in CatalogoServicios.Servicios)
            Servicios.Add(new Servicio
            {
                Id = 100 + Servicios.Count, CodigoCatalogo = definicion.Codigo, Nombre = definicion.Nombre,
                Descripcion = "Servicio del catálogo", Costo = definicion.PrecioInicial, UnidadCantidad = definicion.UnidadCantidad,
                TiempoEstimadoHoras = 1m, CreadoPor = "autor_original", FechaCreacion = new DateTime(2026, 11, 1)
            });
        Servicios.Add(new Servicio { Id = 250, CodigoCatalogo = "CAMBIO_FILTROS", Nombre = "Unidad antigua incompatible", Costo = 99, TiempoEstimadoHoras = 1, UnidadCantidad = null });
        Historial.Add(new HistorialCostoServicio
        {
            Id = 1, ServicioId = 7, NombreServicio = "Servicio histórico", CostoAnterior = 45m,
            CostoNuevo = 55.50m, FechaCambio = new DateTime(2026, 11, 1, 9, 30, 0)
        });
        TerminoBusqueda = TerminoSugerencias = null;
        SoloCatalogoEnSugerencias = false;
        Consultas = Escrituras = 0;
        ErrorLectura = ErrorEscritura = null;
    }

    private void Consultar()
    {
        Consultas++;
        if (ErrorLectura is not null) throw ErrorLectura;
    }
    private void Escribir()
    {
        Escrituras++;
        if (ErrorEscritura is not null) throw ErrorEscritura;
    }

    public List<Servicio> GetAll() { Consultar(); return Servicios; }
    public Servicio? GetById(int id) { Consultar(); return Servicios.Find(servicio => servicio.Id == id); }
    public int Count() { Consultar(); return Servicios.Count; }
    public List<Servicio> ObtenerCatalogo() { Consultar(); return Servicios.Where(CatalogoServicios.EsSeleccionable).ToList(); }
    public Servicio? ObtenerDelCatalogo(int id) { Consultar(); return Servicios.Find(servicio => servicio.Id == id && CatalogoServicios.EsSeleccionable(servicio)); }

    public Task<IReadOnlyList<Servicio>> BuscarAsync(string termino, CancellationToken cancelacion = default)
    {
        Consultar(); TerminoBusqueda = termino;
        return Task.FromResult<IReadOnlyList<Servicio>>(Servicios.Where(servicio => $"{servicio.Nombre} {servicio.Descripcion}".Contains(termino, StringComparison.OrdinalIgnoreCase)).ToArray());
    }
    public Task<IReadOnlyList<SugerenciaServicio>> SugerirAsync(string termino, CancellationToken cancelacion = default) =>
        Sugerir(termino, false);
    public Task<IReadOnlyList<SugerenciaServicio>> SugerirCatalogoAsync(string termino, CancellationToken cancelacion = default) =>
        Sugerir(termino, true);
    private Task<IReadOnlyList<SugerenciaServicio>> Sugerir(string termino, bool soloCatalogo)
    {
        Consultar(); TerminoSugerencias = termino; SoloCatalogoEnSugerencias = soloCatalogo;
        return Task.FromResult<IReadOnlyList<SugerenciaServicio>>([new(7, "Servicio histórico", "Revisión previa")]);
    }
    public void Add(Servicio servicio)
    {
        Escribir(); servicio.Id = Servicios.Max(actual => actual.Id) + 1; Servicios.Add(servicio);
    }
    public void Update(Servicio servicio)
    {
        Escribir();
        var anterior = Servicios.Single(actual => actual.Id == servicio.Id);
        // El doble simula el historial generado por el trigger MySQL existente.
        if (anterior.Costo != servicio.Costo)
            Historial.Add(new HistorialCostoServicio
            {
                Id = Historial.Select(cambio => cambio.Id).DefaultIfEmpty().Max() + 1,
                ServicioId = servicio.Id, NombreServicio = servicio.Nombre, CostoAnterior = anterior.Costo,
                CostoNuevo = servicio.Costo, FechaCambio = new DateTime(2026, 11, 6, 10, 0, 0)
            });
        Servicios.RemoveAll(actual => actual.Id == servicio.Id); Servicios.Add(servicio);
    }
    public void Delete(int id) { Escribir(); Servicios.RemoveAll(servicio => servicio.Id == id); }
    IReadOnlyList<HistorialCostoServicio> IHistorialCostoServicioRepository.GetAll()
    { Consultar(); return Historial.OrderByDescending(cambio => cambio.FechaCambio).ThenByDescending(cambio => cambio.Id).ToArray(); }
    public void Registrar(RegistroAuditoria registro) => Auditoria.Add(registro);
    public IReadOnlyList<RegistroAuditoria> ObtenerRecientes(int cantidad) => Auditoria.Take(cantidad).ToArray();
}
