using TallerMecanico.Application.Ports;
using TallerMecanico.Data;
using TallerMecanico.Data.Factories;
using TallerMecanico.Models;
using TallerMecanico.Patterns.FactoryMethod;
using TallerMecanico.Services;
using TallerMecanico.Validators;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();

// Base de datos
builder.Services.AddScoped<
    DatabaseConnectionFactory,
    MySqlConnectionFactory>();

builder.Services.AddScoped<DatabaseInitializer>();

// ======================================================
// FACTORY METHOD - CREADORES CONCRETOS
// ======================================================

builder.Services.AddScoped<CreadorMecanico>();
builder.Services.AddScoped<CreadorVehiculo>();
builder.Services.AddScoped<CreadorServicio>();
builder.Services.AddScoped<CreadorCliente>();

// ======================================================
// REPOSITORIOS CREADOS MEDIANTE FACTORY METHOD
// ======================================================

// Mecánicos
builder.Services.AddScoped<IRepository<Mecanico>>(
    serviceProvider =>
        serviceProvider
            .GetRequiredService<CreadorMecanico>()
            .CrearRepositorio());

// Servicios
builder.Services.AddScoped<IRepository<Servicio>>(
    serviceProvider =>
        serviceProvider
            .GetRequiredService<CreadorServicio>()
            .CrearRepositorio());

// Vehículos
builder.Services.AddScoped<IRepository<Vehiculo>>(
    serviceProvider =>
        serviceProvider
            .GetRequiredService<CreadorVehiculo>()
            .CrearRepositorio());

// Clientes
builder.Services.AddScoped<IRepository<Cliente>>(
    serviceProvider =>
        serviceProvider
            .GetRequiredService<CreadorCliente>()
            .CrearRepositorio());

// ======================================================
// PUERTOS HEXAGONALES
// ======================================================

// ClienteService dependerá del puerto y no de ClienteRepository.
builder.Services.AddScoped<IClientePort>(
    serviceProvider =>
        (IClientePort)serviceProvider
            .GetRequiredService<IRepository<Cliente>>());

// ======================================================
// MECÁNICOS
// ======================================================

builder.Services.AddScoped<ValidacionMecanicos>();
builder.Services.AddScoped<MecanicoService>();

// ======================================================
// SERVICIOS DEL TALLER
// ======================================================

builder.Services.AddScoped<
    IServicioService,
    ServicioService>();

builder.Services.AddScoped<ValidacionServicios>();

// ======================================================
// HISTORIAL DE COSTOS
// ======================================================

builder.Services.AddScoped<
    IHistorialCostoServicioRepository,
    HistorialCostoServicioRepository>();

builder.Services.AddScoped<
    IHistorialCostoServicioService,
    HistorialCostoServicioService>();

// ======================================================
// VEHÍCULOS
// ======================================================

builder.Services.AddScoped<ValidacionVehiculos>();
builder.Services.AddScoped<VehiculoService>();

// ======================================================
// CLIENTES
// ======================================================

builder.Services.AddScoped<ValidacionClientes>();
builder.Services.AddScoped<ClienteService>();

// ======================================================
// DASHBOARD
// ======================================================

builder.Services.AddScoped<DashboardService>();

var app = builder.Build();

// ======================================================
// INICIALIZACIÓN DE BASE DE DATOS
// ======================================================

using (IServiceScope scope = app.Services.CreateScope())
{
    DatabaseInitializer databaseInitializer =
        scope.ServiceProvider
            .GetRequiredService<DatabaseInitializer>();

    databaseInitializer.Initialize();
}

// ======================================================
// HTTP PIPELINE
// ======================================================

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