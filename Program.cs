using TallerMecanico.Infraestructura.Adapters.Gmail;
using TallerMecanico.Infraestructura.Adapters.MySql;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using TallerMecanico.Application.Ports;
using TallerMecanico.Data;
using TallerMecanico.Data.Factories;
using TallerMecanico.Models;
using TallerMecanico.Patterns.FactoryMethod;
using TallerMecanico.Services;
using TallerMecanico.Validators;
using TallerMecanico.Infraestructura.Identity;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;
using TallerMecanico.Infraestructura.Setup;
using TallerMecanico.Infraestructura.Session;
using TallerMecanico.Application;

var builder =
    WebApplication.CreateBuilder(args);


builder.Services.AddRazorPages(options =>
{
    // Todas las páginas requieren iniciar sesión.
    options.Conventions.AuthorizeFolder("/");

    // Páginas públicas de autenticación.
    options.Conventions.AllowAnonymousToPage("/Auth/Login");
    options.Conventions.AllowAnonymousToPage("/Auth/VerificarEmail");
    options.Conventions.AllowAnonymousToPage("/Auth/Registro");
    options.Conventions.AllowAnonymousToPage("/Auth/ReenviarVerificacion");

    // Administración exclusiva.
    options.Conventions.AuthorizeFolder(
        "/Historial",
        "SoloAdministrador");

    options.Conventions.AuthorizeFolder(
        "/Servicios",
        "SoloAdministrador");

    // Páginas disponibles para ambos roles.
    options.Conventions.AuthorizeFolder(
        "/Administracion",
        "SoloAdministrador");
    options.Conventions.AuthorizeFolder(
        "/Clientes",
        "PersonalAutorizado");

    options.Conventions.AuthorizeFolder(
        "/Vehiculos",
        "PersonalAutorizado");

    options.Conventions.AuthorizeFolder("/Ordenes", "PersonalAutorizado");

    options.Conventions.AuthorizeFolder(
        "/Productos",
        "PersonalAutorizado");
    options.Conventions.AuthorizeFolder(
    "/Mecanicos",
    "SoloAdministrador");
});


builder.Services.AddHttpContextAccessor();

builder.Services.AddScoped<ICurrentUser, CurrentUser>();
builder.Services.AddScoped<IBorradorVehiculoPort, BorradorVehiculoSesion>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<ITokenOrdenPort, TokenOrdenSesion>();
builder.Services.AddScoped<IUnidadTrabajoOrdenPort, MySqlUnidadTrabajoOrden>();
builder.Services.AddScoped<ValidacionOrdenes>();
builder.Services.AddScoped<OrdenServicioService>();
builder.Services.AddScoped<OrdenServicioFacade>();

builder.Services.Configure<Argon2idOptions>(
    builder.Configuration.GetSection(
        Argon2idOptions.SectionName));

builder.Services.AddSingleton<
    IPasswordHasher,
    Argon2idPasswordHasher>();

builder.Services.AddSingleton<AuthenticationFallbackHash>();


// =====================================================
// AUTENTICACIÓN DE USUARIOS
// =====================================================

builder.Services.AddScoped<
    IUsuarioPort,
    UsuarioRepository>();

builder.Services.AddScoped<
    ISolicitudRegistroPort,
    SolicitudRegistroRepository>();

builder.Services.AddScoped<RegistroUsuarioService>();

builder.Services.AddScoped<VerificacionEmailService>();

builder.Services.AddScoped<IEmailService, GmailEmailService>();
builder.Services.AddScoped<RegistroConEmailService>();
builder.Services.AddScoped<ReenvioVerificacionService>();

builder.Services.AddScoped<
    IRevisionSolicitudesPort,
    RevisionSolicitudesRepository>();

builder.Services.AddScoped<RevisionSolicitudesService>();

builder.Services.AddScoped<
    IAuthenticationService,
    AuthenticationService>();

builder.Services.AddScoped<
    IAuditoriaPort,
    AuditoriaRepository>();

builder.Services.AddScoped<
    DatabaseConnectionFactory,
    MySqlConnectionFactory>();

builder.Services.AddScoped<
    InicializadorUsuarios>();

builder.Services.AddScoped<
    InicializacionUsuariosConsola>();

builder.Services.AddScoped<
    RestablecimientoAdminConsola>();

builder.Services.AddScoped<
    DatabaseInitializer>();


// =====================================================
// AUTENTICACIÓN POR COOKIES
// =====================================================

builder.Services
    .AddAuthentication(
        CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Auth/Login";
        options.AccessDeniedPath = "/Auth/AccessDenied";

        options.Cookie.Name = "AutoTaller.Auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy =
            CookieSecurePolicy.Always;

        options.ExpireTimeSpan = TimeSpan.FromMinutes(30);
        options.SlidingExpiration = true;
    });


// =====================================================
// AUTORIZACIÓN POR ROLES
// =====================================================

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("SoloAdministrador", policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.RequireRole("Administrador");
    });

    options.AddPolicy("PersonalAutorizado", policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.RequireRole(
            "Administrador",
            "Recepcionista");
    });
});


// =====================================================
// SESSION
// =====================================================

builder.Services.AddDistributedMemoryCache();

builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);

    options.Cookie.Name = "AutoTaller.Session";
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy =
        CookieSecurePolicy.Always;
});


// =====================================================
// PROTECCIÓN CONTRA INTENTOS REPETIDOS DE LOGIN
// =====================================================

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode =
        StatusCodes.Status429TooManyRequests;

    options.AddPolicy("EmailVerificationRateLimit", context =>
    {
        string direccionIp =
            context.Connection.RemoteIpAddress?
                .ToString() ?? "desconocida";

        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: direccionIp,
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            });
    });
    options.AddPolicy("ReenvioRateLimit", context =>
    {
        string direccionIp =
            context.Connection.RemoteIpAddress?
                .ToString() ?? "desconocida";

        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: direccionIp,
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 3,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            });
    });

    options.AddPolicy("RegistroRateLimit", context =>
    {
        string direccionIp =
            context.Connection.RemoteIpAddress?
                .ToString() ?? "desconocida";

        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: direccionIp,
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 3,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            });
    });
    options.AddPolicy("LoginRateLimit", context =>
    {
        string direccionIp =
            context.Connection.RemoteIpAddress?
                .ToString() ?? "desconocida";

        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: direccionIp,
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            });
    });
});



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

// =====================================================
// RESTABLECIMIENTO ADMINISTRATIVO DE CONTRASEÑA
// =====================================================

if (args.Contains(
        "--restablecer-admin",
        StringComparer.OrdinalIgnoreCase))
{
    if (args.Length != 1)
    {
        throw new InvalidOperationException(
            "El comando no admite argumentos adicionales.");
    }

    if (!app.Environment.IsDevelopment())
    {
        throw new InvalidOperationException(
            "El restablecimiento solo está permitido en Development.");
    }

    using (IServiceScope scope =
           app.Services.CreateScope())
    {
        RestablecimientoAdminConsola restablecimiento =
            scope.ServiceProvider
                .GetRequiredService<RestablecimientoAdminConsola>();

        restablecimiento.Ejecutar();
    }

    return;
}

// =====================================================
// INICIALIZACION ADMINISTRATIVA DE USUARIOS
// =====================================================

if (args.Contains(
        "--inicializar-usuarios",
        StringComparer.OrdinalIgnoreCase))
{
    if (args.Length != 1)
    {
        throw new InvalidOperationException(
            "El comando administrativo no admite argumentos adicionales.");
    }

    if (!app.Environment.IsDevelopment())
    {
        throw new InvalidOperationException(
            "La inicialización de usuarios solo está permitida en Development.");
    }

    using (IServiceScope scope =
           app.Services.CreateScope())
    {
        InicializacionUsuariosConsola inicializacion =
            scope.ServiceProvider
                .GetRequiredService<InicializacionUsuariosConsola>();

        inicializacion.Ejecutar();
    }

    return;
}

// =====================================================
// INICIALIZACION NORMAL DE BASE DE DATOS
// =====================================================

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

app.UseRateLimiter();

app.UseSession();

app.UseAuthentication();

app.UseAuthorization();



app.MapStaticAssets();

app.MapRazorPages()
    .WithStaticAssets();

app.Run();
