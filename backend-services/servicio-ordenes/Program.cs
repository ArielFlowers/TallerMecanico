using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TallerMecanico.Aplicacion.CasosUso.Ordenes;
using TallerMecanico.Aplicacion.CasosUso.Ordenes.Contratos;
using TallerMecanico.Aplicacion.Configuracion;
using TallerMecanico.Aplicacion.Puertos;
using TallerMecanico.Aplicacion.Fachadas;
using TallerMecanico.Aplicacion.Servicios;
using TallerMecanico.Aplicacion.Validaciones;
using TallerMecanico.Contratos;
using TallerMecanico.Controllers;
using TallerMecanico.Dominio.Entidades;
using TallerMecanico.Dominio.Servicios;
using TallerMecanico.Infraestructura.Documentos;
using TallerMecanico.Infraestructura.Persistencia;
using TallerMecanico.Infraestructura.Persistencia.Fabricas;
using TallerMecanico.Infraestructura.Persistencia.MySql;
using TallerMecanico.Infraestructura.Seguridad;

// Servicio 1: órdenes y catálogos (Clean Architecture).
var builder = WebApplication.CreateBuilder(args);

// Las reglas de cada campo las valida la aplicación, no el enlace de MVC.
builder.Services.AddControllers(opciones =>
{
    opciones.Filters.Add<ErroresApiFilter>();
    opciones.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true;
}).ConfigureApiBehaviorOptions(opciones =>
{
    opciones.InvalidModelStateResponseFactory = _ =>
        new BadRequestObjectResult(new ErrorApi("Los datos de la solicitud no son válidos."));
});
builder.Services.AddHttpContextAccessor();

// =====================================================
// ACCESO INTERNO: SOLO EL FRONTEND PUEDE LLAMAR A ESTA API
// =====================================================
builder.Services
    .AddAuthentication(ClaveInternaHandler.Esquema)
    .AddScheme<AuthenticationSchemeOptions, ClaveInternaHandler>(ClaveInternaHandler.Esquema, null);
builder.Services.AddAuthorization(opciones =>
    opciones.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());
builder.Services.AddScoped<ICurrentUser, CurrentUser>();

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton(ConfiguracionTaller.Instancia);
builder.Services.AddScoped<DatabaseConnectionFactory, MySqlConnectionFactory>();
builder.Services.AddScoped<IAuditoriaPort, AuditoriaRepository>();
builder.Services.AddScoped<DatabaseInitializer>();

// =====================================================
// ÓRDENES: CASOS DE USO Y ADAPTADORES
// =====================================================
builder.Services.AddScoped<IUnidadTrabajoOrden, MySqlUnidadTrabajoOrden>();
builder.Services.AddScoped<ValidacionOrdenes>();
builder.Services.AddScoped<CrearOrdenCasoUso>();
builder.Services.AddScoped<AnularOrdenCasoUso>();
builder.Services.AddScoped<OrdenServicioFacade>();
builder.Services.AddScoped<CreadorConsultaOrdenes>();
builder.Services.AddScoped<IOrdenQueryRepository>(proveedor => proveedor.GetRequiredService<CreadorConsultaOrdenes>().CrearRepositorio());
builder.Services.AddScoped<ConsultarOrdenesCasoUso>();
builder.Services.AddScoped<ConsultaOrdenesFacade>();
builder.Services.AddSingleton<ImporteEnPalabras>();
builder.Services.AddScoped<IComprobantePdf, ComprobanteOrdenPdf>();
builder.Services.AddScoped<GenerarComprobanteCasoUso>();
QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

// =====================================================
// FACTORY METHOD
// =====================================================
builder.Services.AddScoped<CreadorMecanico>();
builder.Services.AddScoped<CreadorVehiculo>();
builder.Services.AddScoped<CreadorServicio>();
builder.Services.AddScoped<CreadorCliente>();
builder.Services.AddScoped<CreadorProducto>();
builder.Services.AddScoped<IRepository<Mecanico>>(proveedor => proveedor.GetRequiredService<CreadorMecanico>().CrearRepositorio());
builder.Services.AddScoped<IRepository<Servicio>>(proveedor => proveedor.GetRequiredService<CreadorServicio>().CrearRepositorio());
builder.Services.AddScoped<IRepository<Vehiculo>>(proveedor => proveedor.GetRequiredService<CreadorVehiculo>().CrearRepositorio());
builder.Services.AddScoped<IRepository<Cliente>>(proveedor => proveedor.GetRequiredService<CreadorCliente>().CrearRepositorio());
builder.Services.AddScoped<IRepository<Producto>>(proveedor => proveedor.GetRequiredService<CreadorProducto>().CrearRepositorio());
builder.Services.AddScoped<IConsultaMecanicosPort>(proveedor => (IConsultaMecanicosPort)proveedor.GetRequiredService<IRepository<Mecanico>>());
builder.Services.AddScoped<IConsultaServiciosPort>(proveedor => (IConsultaServiciosPort)proveedor.GetRequiredService<IRepository<Servicio>>());
builder.Services.AddScoped<IClientePort>(proveedor => (IClientePort)proveedor.GetRequiredService<IRepository<Cliente>>());
builder.Services.AddScoped<IVehiculoPort>(proveedor => (IVehiculoPort)proveedor.GetRequiredService<IRepository<Vehiculo>>());
builder.Services.AddScoped<IProductoPort>(proveedor => (IProductoPort)proveedor.GetRequiredService<IRepository<Producto>>());

// =====================================================
// CATÁLOGOS Y DASHBOARD
// =====================================================
builder.Services.AddScoped<ValidacionMecanicos>();
builder.Services.AddScoped<MecanicoService>();
builder.Services.AddScoped<IServicioService, ServicioService>();
builder.Services.AddScoped<ValidacionServicios>();
builder.Services.AddScoped<IHistorialCostoServicioRepository, HistorialCostoServicioRepository>();
builder.Services.AddScoped<IHistorialCostoServicioService, HistorialCostoServicioService>();
builder.Services.AddScoped<ValidacionVehiculos>();
builder.Services.AddScoped<VehiculoService>();
builder.Services.AddScoped<ValidacionClientes>();
builder.Services.AddScoped<ClienteService>();
builder.Services.AddScoped<ValidacionProductos>();
builder.Services.AddScoped<ProductoService>();
builder.Services.AddScoped<DashboardService>();

var app = builder.Build();

// =====================================================
// ESQUEMA DE LA BASE COMPARTIDA
// =====================================================
int intentosInicio = int.TryParse(app.Configuration["Database:IntentosInicio"], out int intentos) && intentos > 0 ? intentos : 10;
int esperaSegundos = int.TryParse(app.Configuration["Database:EsperaEntreIntentosSegundos"], out int espera) && espera >= 0 ? espera : 5;

for (int intento = 1; ; intento++)
{
    try
    {
        using IServiceScope scope = app.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<DatabaseInitializer>().Initialize();
        break;
    }
    catch (Exception ex) when (intento < intentosInicio && ex is System.Data.Common.DbException or TimeoutException)
    {
        app.Logger.LogWarning(
            "Base de datos no disponible (intento {Intento}/{Intentos}). Se reintentará en {EsperaSegundos} segundos.",
            intento, intentosInicio, esperaSegundos);
        Thread.Sleep(TimeSpan.FromSeconds(esperaSegundos));
    }
}

// Permite crear el esquema sin levantar el servidor.
if (args.Contains("--inicializar-esquema", StringComparer.OrdinalIgnoreCase))
{
    return;
}

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/salud", () => Results.Ok()).AllowAnonymous();
app.MapControllers();

app.Run();
