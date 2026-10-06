using TallerMecanico.Application.Ports;
using TallerMecanico.Data;
using TallerMecanico.Data.Factories;
using TallerMecanico.Models;
using TallerMecanico.Patterns.FactoryMethod;
using TallerMecanico.Services;
using TallerMecanico.Validators;

var builder =
    WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();

builder.Services.AddScoped<
    DatabaseConnectionFactory,
    MySqlConnectionFactory>();

builder.Services.AddScoped<
    DatabaseInitializer>();

// =====================================================
// FACTORY METHOD
// =====================================================

builder.Services.AddScoped<CreadorMecanico>();
builder.Services.AddScoped<CreadorVehiculo>();
builder.Services.AddScoped<CreadorServicio>();
builder.Services.AddScoped<CreadorCliente>();
builder.Services.AddScoped<CreadorProducto>();

builder.Services.AddScoped<IRepository<Mecanico>>(
    serviceProvider =>
        serviceProvider
            .GetRequiredService<CreadorMecanico>()
            .CrearRepositorio());

builder.Services.AddScoped<IRepository<Servicio>>(
    serviceProvider =>
        serviceProvider
            .GetRequiredService<CreadorServicio>()
            .CrearRepositorio());

builder.Services.AddScoped<IRepository<Vehiculo>>(
    serviceProvider =>
        serviceProvider
            .GetRequiredService<CreadorVehiculo>()
            .CrearRepositorio());

builder.Services.AddScoped<IRepository<Cliente>>(
    serviceProvider =>
        serviceProvider
            .GetRequiredService<CreadorCliente>()
            .CrearRepositorio());

builder.Services.AddScoped<IRepository<Producto>>(
    serviceProvider =>
        serviceProvider
            .GetRequiredService<CreadorProducto>()
            .CrearRepositorio());

// =====================================================
// PUERTOS HEXAGONALES
// =====================================================

builder.Services.AddScoped<IClientePort>(
    serviceProvider =>
        (IClientePort)
        serviceProvider
            .GetRequiredService<IRepository<Cliente>>());

builder.Services.AddScoped<IVehiculoPort>(
    serviceProvider =>
        (IVehiculoPort)
        serviceProvider
            .GetRequiredService<IRepository<Vehiculo>>());

builder.Services.AddScoped<IProductoPort>(
    serviceProvider =>
        (IProductoPort)
        serviceProvider
            .GetRequiredService<IRepository<Producto>>());

// =====================================================
// MECÁNICOS
// =====================================================

builder.Services.AddScoped<ValidacionMecanicos>();
builder.Services.AddScoped<MecanicoService>();

// =====================================================
// SERVICIOS
// =====================================================

builder.Services.AddScoped<
    IServicioService,
    ServicioService>();

builder.Services.AddScoped<
    ValidacionServicios>();

// =====================================================
// HISTORIAL
// =====================================================

builder.Services.AddScoped<
    IHistorialCostoServicioRepository,
    HistorialCostoServicioRepository>();

builder.Services.AddScoped<
    IHistorialCostoServicioService,
    HistorialCostoServicioService>();

// =====================================================
// VEHÍCULOS
// =====================================================

builder.Services.AddScoped<
    ValidacionVehiculos>();

builder.Services.AddScoped<
    VehiculoService>();

// =====================================================
// CLIENTES
// =====================================================

builder.Services.AddScoped<
    ValidacionClientes>();

builder.Services.AddScoped<
    ClienteService>();

// =====================================================
// PRODUCTOS
// =====================================================

builder.Services.AddScoped<
    ValidacionProductos>();

builder.Services.AddScoped<
    ProductoService>();

// =====================================================
// DASHBOARD
// =====================================================

builder.Services.AddScoped<
    DashboardService>();

var app =
    builder.Build();

using (IServiceScope scope =
       app.Services.CreateScope())
{
    DatabaseInitializer databaseInitializer =
        scope.ServiceProvider
            .GetRequiredService<DatabaseInitializer>();

    databaseInitializer.Initialize();
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseRouting();

app.UseAuthorization();

app.MapStaticAssets();

app.MapRazorPages()
    .WithStaticAssets();

app.Run();