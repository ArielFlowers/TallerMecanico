using System.Data.Common;
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

public sealed class ClientesVehiculosApiFixture : IAsyncLifetime
{
    private WebApplication? _app;
    internal PuertosCatalogosApi Puertos { get; } = new();
    public HttpClient Http { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = "Testing",
            ContentRootPath = AppContext.BaseDirectory,
            ApplicationName = typeof(ClientesController).Assembly.GetName().Name
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
        }).AddApplicationPart(typeof(ClientesController).Assembly);
        builder.Services.AddAuthentication(ClaveInternaHandler.Esquema)
            .AddScheme<AuthenticationSchemeOptions, ClaveInternaHandler>(ClaveInternaHandler.Esquema, null);
        builder.Services.AddAuthorization(opciones =>
            opciones.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddScoped<ICurrentUser, CurrentUser>();
        builder.Services.AddSingleton<IClientePort>(Puertos);
        builder.Services.AddSingleton<IVehiculoPort>(Puertos);
        builder.Services.AddSingleton<IAuditoriaPort>(Puertos);
        builder.Services.AddScoped<ValidacionClientes>();
        builder.Services.AddScoped<ValidacionVehiculos>();
        builder.Services.AddScoped<ClienteService>();
        builder.Services.AddScoped<VehiculoService>();

        // Se aloja el MVC real con puertos simulados; no se ejecuta el inicializador MySQL de Program.
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

internal sealed class PuertosCatalogosApi : IClientePort, IVehiculoPort, IAuditoriaPort
{
    public List<Cliente> Clientes { get; } = [];
    public List<Vehiculo> Vehiculos { get; } = [];
    public List<RegistroAuditoria> Auditoria { get; } = [];
    public string? BusquedaClientes { get; private set; }
    public string? BusquedaVehiculos { get; private set; }
    public string? SugerenciasClientes { get; private set; }
    public string? SugerenciasVehiculos { get; private set; }
    public int Consultas { get; private set; }
    public int Escrituras { get; private set; }
    public Exception? ErrorLectura { get; set; }
    public Exception? ErrorEscritura { get; set; }
    public bool VehiculoConOrdenes { get; set; }

    public void Reiniciar()
    {
        Clientes.Clear(); Vehiculos.Clear(); Auditoria.Clear();
        Clientes.Add(new Cliente { Id = 7, Ci = "1234567", ComplementoCi = "1A", Nombres = "Ana", PrimerApellido = "Prueba", SegundoApellido = "Pérez", Celular = "71234567", CreadoPor = "autor_original", FechaCreacion = new DateTime(2026, 11, 1) });
        Clientes.Add(new Cliente { Id = 8, Ci = "7654321", Nombres = "José", PrimerApellido = "Muñoz", SegundoApellido = "Rojas", Celular = "61234567", CreadoPor = "autor_original" });
        Vehiculos.Add(new Vehiculo { Id = 7, Placa = "123ABC", Marca = "Toyota", Modelo = "Corolla", ClienteId = 7, Kilometraje = 1200, Observaciones = "Vehículo de prueba", CreadoPor = "autor_original", FechaCreacion = new DateTime(2026, 11, 1) });
        BusquedaClientes = BusquedaVehiculos = SugerenciasClientes = SugerenciasVehiculos = null;
        Consultas = Escrituras = 0;
        ErrorLectura = ErrorEscritura = null;
        VehiculoConOrdenes = false;
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

    IReadOnlyList<Cliente> IClientePort.GetAll() { Consultar(); return Clientes; }
    IReadOnlyList<Vehiculo> IVehiculoPort.GetAll() { Consultar(); return Vehiculos; }
    Cliente? IClientePort.GetById(int id) { Consultar(); return Clientes.Find(cliente => cliente.Id == id); }
    Vehiculo? IVehiculoPort.GetById(int id) { Consultar(); return Vehiculos.Find(vehiculo => vehiculo.Id == id); }

    IReadOnlyList<Cliente> IClientePort.Search(string termino)
    {
        Consultar(); BusquedaClientes = termino;
        return Clientes.Where(cliente => $"{cliente.Ci} {cliente.Nombres} {cliente.PrimerApellido} {cliente.SegundoApellido}".Contains(termino, StringComparison.OrdinalIgnoreCase)).ToArray();
    }

    IReadOnlyList<Vehiculo> IVehiculoPort.Search(string termino)
    {
        Consultar(); BusquedaVehiculos = termino;
        return Vehiculos.Where(vehiculo => $"{vehiculo.Placa} {vehiculo.Marca} {vehiculo.Modelo}".Contains(termino, StringComparison.OrdinalIgnoreCase)).ToArray();
    }

    Task<IReadOnlyList<SugerenciaCliente>> IClientePort.SugerirAsync(string termino, CancellationToken cancelacion)
    {
        Consultar(); SugerenciasClientes = termino;
        return Task.FromResult<IReadOnlyList<SugerenciaCliente>>([new(7, "Ana Prueba Pérez", "CI: 1234567-1A")]);
    }

    Task<IReadOnlyList<SugerenciaVehiculo>> IVehiculoPort.SugerirAsync(string termino, CancellationToken cancelacion)
    {
        Consultar(); SugerenciasVehiculos = termino;
        return Task.FromResult<IReadOnlyList<SugerenciaVehiculo>>([new(7, "123ABC", "Toyota Corolla")]);
    }

    bool IClientePort.ExistsByCi(string ci, string complemento, int excluido)
    { Consultar(); return Clientes.Any(cliente => cliente.Ci == ci && cliente.ComplementoCi == complemento && cliente.Id != excluido); }
    bool IVehiculoPort.ExistsByPlaca(string placa, int excluido)
    { Consultar(); return Vehiculos.Any(vehiculo => vehiculo.Placa == placa && vehiculo.Id != excluido); }
    bool IVehiculoPort.ExistsPorCliente(int id)
    { Consultar(); return Vehiculos.Any(vehiculo => vehiculo.ClienteId == id); }

    void IClientePort.Add(Cliente cliente)
    { Escribir(); cliente.Id = Clientes.Select(actual => actual.Id).DefaultIfEmpty().Max() + 1; Clientes.Add(cliente); }
    void IVehiculoPort.Add(Vehiculo vehiculo)
    { Escribir(); vehiculo.Id = Vehiculos.Select(actual => actual.Id).DefaultIfEmpty().Max() + 1; Vehiculos.Add(vehiculo); }
    void IClientePort.Update(Cliente cliente)
    { Escribir(); Clientes.RemoveAll(actual => actual.Id == cliente.Id); Clientes.Add(cliente); }
    void IVehiculoPort.Update(Vehiculo vehiculo)
    { Escribir(); Vehiculos.RemoveAll(actual => actual.Id == vehiculo.Id); Vehiculos.Add(vehiculo); }
    void IClientePort.Delete(int id)
    { Escribir(); Clientes.RemoveAll(cliente => cliente.Id == id); }
    void IVehiculoPort.Delete(int id)
    {
        if (VehiculoConOrdenes) { Escrituras++; throw new RegistroRelacionadoException(new InvalidOperationException()); }
        Escribir(); Vehiculos.RemoveAll(vehiculo => vehiculo.Id == id);
    }
    public void Registrar(RegistroAuditoria registro) => Auditoria.Add(registro);
    public IReadOnlyList<RegistroAuditoria> ObtenerRecientes(int cantidad) => Auditoria.Take(cantidad).ToArray();
}

internal sealed class BaseDatosApiException() : DbException("Detalle de persistencia que no debe salir de la API.");
