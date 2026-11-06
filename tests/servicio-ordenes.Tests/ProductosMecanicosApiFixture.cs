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
using TallerMecanico.Aplicacion.Errores;
using TallerMecanico.Aplicacion.Puertos;
using TallerMecanico.Aplicacion.Servicios;
using TallerMecanico.Aplicacion.Validaciones;
using TallerMecanico.Controllers;
using TallerMecanico.Dominio.Entidades;
using TallerMecanico.Infraestructura.Seguridad;

namespace TallerMecanico.IntegrationTests;

public sealed class ProductosMecanicosApiFixture : IAsyncLifetime
{
    private WebApplication? _app;
    internal PuertosProductosMecanicosApi Puertos { get; } = new();
    public HttpClient Http { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = "Testing",
            ContentRootPath = AppContext.BaseDirectory,
            ApplicationName = typeof(ProductosController).Assembly.GetName().Name
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
        }).AddApplicationPart(typeof(ProductosController).Assembly);
        builder.Services.AddAuthentication(ClaveInternaHandler.Esquema)
            .AddScheme<AuthenticationSchemeOptions, ClaveInternaHandler>(ClaveInternaHandler.Esquema, null);
        builder.Services.AddAuthorization(opciones =>
            opciones.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddScoped<ICurrentUser, CurrentUser>();
        builder.Services.AddSingleton<IProductoPort>(Puertos);
        builder.Services.AddSingleton<IRepository<Mecanico>>(Puertos);
        builder.Services.AddSingleton<IConsultaMecanicosPort>(Puertos);
        builder.Services.AddSingleton<IAuditoriaPort>(Puertos);
        builder.Services.AddScoped<ValidacionProductos>();
        builder.Services.AddScoped<ValidacionMecanicos>();
        builder.Services.AddScoped<ProductoService>();
        builder.Services.AddScoped<MecanicoService>();

        // MVC, autenticación y Aplicación reales, sin iniciar Program ni su conexión MySQL.
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

internal sealed class PuertosProductosMecanicosApi : IProductoPort, IRepository<Mecanico>, IConsultaMecanicosPort, IAuditoriaPort
{
    public List<Producto> Productos { get; } = [];
    public List<Mecanico> Mecanicos { get; } = [];
    public List<RegistroAuditoria> Auditoria { get; } = [];
    public string? BusquedaProductos { get; private set; }
    public string? BusquedaMecanicos { get; private set; }
    public string? SugerenciasProductos { get; private set; }
    public string? SugerenciasMecanicos { get; private set; }
    public bool IncluyeEspecialidad { get; private set; }
    public int Consultas { get; private set; }
    public int Escrituras { get; private set; }
    public int MovimientosStock { get; private set; }
    public Exception? ErrorLectura { get; set; }
    public Exception? ErrorEscritura { get; set; }
    public bool TieneOrdenes { get; set; }

    public void Reiniciar()
    {
        Productos.Clear(); Mecanicos.Clear(); Auditoria.Clear();
        Productos.Add(new Producto { Id = 7, Codigo = "FIL-001", Nombre = "Filtro de aceite", Precio = 25.50m, Stock = 10, StockMinimo = 3, CreadoPor = "autor_original", FechaCreacion = new DateTime(2026, 11, 1) });
        Productos.Add(new Producto { Id = 8, Codigo = "BUJ-002", Nombre = "Bujía", Precio = 40, Stock = 5, StockMinimo = 1, CreadoPor = "autor_original" });
        Mecanicos.Add(new Mecanico { Id = 7, Ci = "1234567", ComplementoCi = "1A", Nombres = "José", PrimerApellido = "Prueba", SegundoApellido = "", Genero = "Masculino", Especialidad = "Motores", Celular = "71234567", CreadoPor = "autor_original", FechaCreacion = new DateTime(2026, 11, 1) });
        Mecanicos.Add(new Mecanico { Id = 8, Ci = "7654321", Nombres = "María", PrimerApellido = "Pérez", Especialidad = "Electricidad Automotriz" });
        BusquedaProductos = BusquedaMecanicos = SugerenciasProductos = SugerenciasMecanicos = null;
        Consultas = Escrituras = MovimientosStock = 0;
        ErrorLectura = ErrorEscritura = null;
        TieneOrdenes = IncluyeEspecialidad = false;
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
    private void EliminarRelacionado()
    {
        Escribir();
        if (TieneOrdenes) throw new RegistroRelacionadoException(new InvalidOperationException());
    }

    IReadOnlyList<Producto> IProductoPort.GetAll() { Consultar(); return Productos; }
    List<Mecanico> IRepository<Mecanico>.GetAll() { Consultar(); return Mecanicos; }
    Producto? IProductoPort.GetById(int id) { Consultar(); return Productos.Find(producto => producto.Id == id); }
    Mecanico? IRepository<Mecanico>.GetById(int id) { Consultar(); return Mecanicos.Find(mecanico => mecanico.Id == id); }
    int IRepository<Mecanico>.Count() { Consultar(); return Mecanicos.Count; }

    IReadOnlyList<Producto> IProductoPort.Search(string termino)
    {
        Consultar(); BusquedaProductos = termino;
        return Productos.Where(producto => $"{producto.Codigo} {producto.Nombre}".Contains(termino, StringComparison.OrdinalIgnoreCase)).ToArray();
    }
    List<Mecanico> IConsultaMecanicosPort.Search(string termino)
    {
        Consultar(); BusquedaMecanicos = termino;
        return Mecanicos.Where(mecanico => $"{mecanico.Ci} {mecanico.Nombres} {mecanico.PrimerApellido} {mecanico.Especialidad}".Contains(termino, StringComparison.OrdinalIgnoreCase)).ToList();
    }
    Task<IReadOnlyList<SugerenciaProducto>> IProductoPort.SugerirAsync(string termino, CancellationToken cancelacion)
    {
        Consultar(); SugerenciasProductos = termino;
        return Task.FromResult<IReadOnlyList<SugerenciaProducto>>([new(7, "Filtro de aceite", "FIL-001") { Stock = 10 }]);
    }
    Task<IReadOnlyList<SugerenciaMecanico>> IConsultaMecanicosPort.SugerirAsync(string termino, CancellationToken cancelacion, bool incluirEspecialidad)
    {
        Consultar(); SugerenciasMecanicos = termino; IncluyeEspecialidad = incluirEspecialidad;
        return Task.FromResult<IReadOnlyList<SugerenciaMecanico>>([new(7, "José Prueba", "CI: 1234567-1A · Motores")]);
    }
    bool IProductoPort.ExistsByCodigo(string codigo, int excluido)
    { Consultar(); return Productos.Any(producto => producto.Codigo == codigo && producto.Id != excluido); }
    bool IConsultaMecanicosPort.ExistsByCi(string ci, string complemento, int excluido)
    { Consultar(); return Mecanicos.Any(mecanico => mecanico.Ci == ci && mecanico.ComplementoCi == complemento && mecanico.Id != excluido); }

    void IProductoPort.Add(Producto producto)
    { Escribir(); producto.Id = Productos.Select(actual => actual.Id).DefaultIfEmpty().Max() + 1; Productos.Add(producto); }
    void IRepository<Mecanico>.Add(Mecanico mecanico)
    { Escribir(); mecanico.Id = Mecanicos.Select(actual => actual.Id).DefaultIfEmpty().Max() + 1; Mecanicos.Add(mecanico); }
    void IProductoPort.Update(Producto producto)
    { Escribir(); Productos.RemoveAll(actual => actual.Id == producto.Id); Productos.Add(producto); }
    void IRepository<Mecanico>.Update(Mecanico mecanico)
    { Escribir(); Mecanicos.RemoveAll(actual => actual.Id == mecanico.Id); Mecanicos.Add(mecanico); }
    void IProductoPort.Delete(int id)
    { EliminarRelacionado(); Productos.RemoveAll(producto => producto.Id == id); }
    void IRepository<Mecanico>.Delete(int id)
    { EliminarRelacionado(); Mecanicos.RemoveAll(mecanico => mecanico.Id == id); }
    bool IProductoPort.Descontar(int id, int cantidad)
    { MovimientosStock++; throw new InvalidOperationException("El CRUD no debe descontar inventario de una orden."); }
    void IProductoPort.Restituir(int id, int cantidad)
    { MovimientosStock++; throw new InvalidOperationException("El CRUD no debe restituir inventario de una orden."); }
    public void Registrar(RegistroAuditoria registro) => Auditoria.Add(registro);
    public IReadOnlyList<RegistroAuditoria> ObtenerRecientes(int cantidad) => Auditoria.Take(cantidad).ToArray();
}
