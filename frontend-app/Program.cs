using TallerMecanico.Infraestructura.Correo;
using TallerMecanico.Infraestructura.Persistencia.MySql;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Http;
using TallerMecanico.Api;
using TallerMecanico.Aplicacion.Puertos;
using TallerMecanico.Infraestructura;
using TallerMecanico.Infraestructura.Persistencia;
using TallerMecanico.Dominio.Entidades;
using TallerMecanico.Infraestructura.Persistencia.Fabricas;
using TallerMecanico.Infraestructura.Documentos;
using TallerMecanico.Infraestructura.Exportacion;
using TallerMecanico.Aplicacion.Servicios;
using TallerMecanico.Aplicacion.Servicios.Reportes;
using TallerMecanico.Aplicacion.Validaciones;
using TallerMecanico.Infraestructura.Seguridad;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;
using TallerMecanico.Infraestructura.Arranque;
using TallerMecanico.Infraestructura.Sesion;
using TallerMecanico.Aplicacion.Fachadas;
using TallerMecanico.Aplicacion.Configuracion;

var builder =
    WebApplication.CreateBuilder(args);

// =====================================================
// H1: CABECERAS DEL PROXY INVERSO NGINX
// =====================================================
// Nginx termina HTTPS y se comunica por HTTP con Kestrel.
// Confiamos exclusivamente en la red Docker de desarrollo
// detectada con: docker network inspect tallermecanico_default.
// Si Docker cambia de subred, actualizar este valor.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders =
        ForwardedHeaders.XForwardedFor |
        ForwardedHeaders.XForwardedProto;
    options.ForwardLimit = 1;
    options.KnownIPNetworks.Add(
        System.Net.IPNetwork.Parse("172.19.0.0/16"));
});

// =====================================================
// H1: DATA PROTECTION COMPARTIDA ENTRE RÉPLICAS
// =====================================================
// Se configura DataProtection__KeysPath en Docker Compose.
// Las 3 réplicas usan el mismo volumen para sus claves.
var dataProtection = builder.Services
    .AddDataProtection()
    .SetApplicationName("AutoTallerPro");

string? directorioClaves =
    builder.Configuration["DataProtection:KeysPath"];

if (!string.IsNullOrWhiteSpace(directorioClaves))
{
    Directory.CreateDirectory(directorioClaves);
    dataProtection.PersistKeysToFileSystem(
        new DirectoryInfo(directorioClaves));
}

// En Development solamente: permitir cookies sobre HTTP para
// pruebas en localhost:80 y localhost:5000-5002.
// En cualquier otro entorno las cookies exigen HTTPS.
var politicaCookieSegura = builder.Environment.IsDevelopment()
    ? CookieSecurePolicy.SameAsRequest
    : CookieSecurePolicy.Always;

builder.Services.AddRazorPages(options =>
{
    // Todas las páginas requieren iniciar sesión.
    options.Conventions.AuthorizeFolder("/");
    // Páginas públicas de autenticación.
    options.Conventions.AllowAnonymousToPage("/Auth/Login");
    options.Conventions.AllowAnonymousToPage("/Auth/VerificarEmail");
    options.Conventions.AllowAnonymousToPage("/Auth/ActivarInvitacion");
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
    "PersonalAutorizado");
    options.Conventions.AuthorizeFolder(
        "/Reportes",
        "SoloAdministrador");
}).AddMvcOptions(options => options.Filters.Add<ConfirmarSesionFilter>());
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();
builder.Services.AddScoped<IBorradorVehiculoPort, BorradorVehiculoSesion>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton(ConfiguracionTaller.Instancia);
builder.Services.AddInfraestructuraOrdenes();
builder.Services.Configure<Argon2idOptions>(
    builder.Configuration.GetSection(
        Argon2idOptions.SectionName));
builder.Services.AddSingleton<
    IPasswordHasher,
    Argon2idPasswordHasher>();
builder.Services.AddSingleton<IFallbackHash, AuthenticationFallbackHash>();
// =====================================================
// AUTENTICACIÓN DE USUARIOS
// =====================================================
builder.Services.AddScoped<
    IUsuarioPort,
    UsuarioRepository>();
builder.Services.AddScoped<
    IUsuarioConsultaPort,
    UsuarioRepository>();
builder.Services.AddScoped<
    ISolicitudRegistroPort,
    SolicitudRegistroRepository>();
builder.Services.AddScoped<RegistroUsuarioService>();
builder.Services.AddScoped<VerificacionEmailService>();
builder.Services.AddScoped<IEmailService, GmailEmailService>();
builder.Services.AddScoped<RegistroFacade>();
builder.Services.AddScoped<IGeneradorNombreUsuarioPort, GeneradorNombreUsuarioService>();
builder.Services.AddScoped<ReenvioVerificacionService>();
builder.Services.AddScoped<
    IRevisionSolicitudesPort,
    RevisionSolicitudesRepository>();
builder.Services.AddScoped<RevisionSolicitudesService>();
builder.Services.AddScoped<
    IUsuarioAdministracionPort,
    UsuarioRepository>();

// Activación de invitaciones (Servicio 2).
builder.Services.AddScoped<
    IActivacionInvitacionPort,
    ActivacionInvitacionMySqlAdapter>();

builder.Services.AddScoped<IGestionUsuariosPort, UsuarioAdministracionService>();
builder.Services.AddScoped<ICambioContrasenaPort, CambioContrasenaService>();
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
            politicaCookieSegura;
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
// SESIÓN EN MEMORIA
// =====================================================
// Borradores y tokens de órdenes. Hay una sola instancia del Frontend,
// así que la sesión puede vivir en su memoria.
builder.Services.AddDistributedMemoryCache();

// =====================================================
// SESSION
// =====================================================
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.Name = "AutoTaller.Session";
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy =
        politicaCookieSegura;
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
    options.OnRejected = (contexto, cancelacion) =>
    {
        contexto.HttpContext.Response.ContentType = "text/plain; charset=utf-8";
        return new ValueTask(contexto.HttpContext.Response.WriteAsync(
            "Demasiados intentos. Espera un minuto e inténtalo de nuevo.", cancelacion));
    };
    options.AddPolicy("LoginRateLimit", context =>
    {
        // Solo cuentan los intentos de inicio de sesión, no abrir la página.
        if (!HttpMethods.IsPost(context.Request.Method))
        {
            return RateLimitPartition.GetNoLimiter("login-lectura");
        }

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
builder.Services.AddScoped<IConsultaMecanicosPort>(
    serviceProvider =>
        (IConsultaMecanicosPort)serviceProvider.GetRequiredService<IRepository<Mecanico>>());
builder.Services.AddScoped<IConsultaServiciosPort>(
    serviceProvider =>
        (IConsultaServiciosPort)serviceProvider.GetRequiredService<IRepository<Servicio>>());
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
// =====================================================
// REPORTES (HU3 - Servicio 3)
// =====================================================
builder.Services.AddScoped<IConsultaReportesPort, ConsultaReportesMySqlAdapter>();
builder.Services.AddScoped<IGeneradorReporteOrdenesPdfPort, QuestPdfReporteOrdenesAdapter>();
builder.Services.AddScoped<IGeneradorReporteOrdenesExcelPort, ClosedXmlReporteOrdenesAdapter>();
builder.Services.AddScoped<IGeneradorReporteMecanicosPdfPort, QuestPdfReporteMecanicosAdapter>();
builder.Services.AddScoped<IGeneradorReporteMecanicosExcelPort, ClosedXmlReporteMecanicosAdapter>();
builder.Services.AddScoped<IReporteOrdenesPort, ReporteOrdenesService>();
builder.Services.AddScoped<IReporteMecanicosPort, ReporteMecanicosService>();
// =====================================================
// CLIENTES HTTP DE LOS SERVICIOS BACKEND
// =====================================================
builder.Services.AddApisInternas(builder.Configuration);

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
// CREACIÓN DEL ESQUEMA SIN LEVANTAR EL SERVIDOR
// =====================================================
// Lo usan las pruebas. En A3 pasa a servicio-ordenes junto con DatabaseInitializer.
if (args.Contains("--inicializar-esquema", StringComparer.OrdinalIgnoreCase))
{
    using IServiceScope scope = app.Services.CreateScope();
    scope.ServiceProvider.GetRequiredService<DatabaseInitializer>().Initialize();
    return;
}

// =====================================================
// INICIALIZACION NORMAL DE BASE DE DATOS
// =====================================================
int intentosInicio = 10;
if (int.TryParse(
        app.Configuration["Database:IntentosInicio"],
        out int intentosConfigurados) &&
    intentosConfigurados > 0)
{
    intentosInicio = intentosConfigurados;
}

int esperaEntreIntentosSegundos = 5;
if (int.TryParse(
        app.Configuration["Database:EsperaEntreIntentosSegundos"],
        out int esperaConfigurada) &&
    esperaConfigurada >= 0)
{
    esperaEntreIntentosSegundos = esperaConfigurada;
}

for (int intento = 1; ; intento++)
{
    try
    {
        using IServiceScope scope =
            app.Services.CreateScope();
        DatabaseInitializer databaseInitializer =
            scope.ServiceProvider
                .GetRequiredService<DatabaseInitializer>();
        databaseInitializer.Initialize();
        break;
    }
    catch (Exception ex) when (
        intento < intentosInicio &&
        ex is System.Data.Common.DbException or TimeoutException)
    {
        app.Logger.LogWarning(
            "Base de datos no disponible (intento {Intento}/{Intentos}). " +
            "Se reintentara en {EsperaSegundos} segundos.",
            intento,
            intentosInicio,
            esperaEntreIntentosSegundos);

        if (esperaEntreIntentosSegundos > 0)
        {
            Thread.Sleep(
                TimeSpan.FromSeconds(
                    esperaEntreIntentosSegundos));
        }
    }
}

// Debe ejecutarse antes de HSTS, redirecciones y autenticación,
// para que Request.Scheme refleje el HTTPS recibido por Nginx.
app.UseForwardedHeaders();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}
// En Development mantenemos HTTP local para las pruebas de H1.
// Nginx también acepta HTTPS con mkcert en el puerto 443.
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseRouting();
app.UseRateLimiter();
app.UseSession();
app.UseAuthentication();
app.UseValidarUsuarioActivo();
app.UseAuthorization();
// H1: endpoint liviano para comprobar balanceo y réplica real.
// No reemplaza la identificación visible que agregaremos en Index.
if (app.Environment.IsDevelopment())
{
    app.MapGet("/h1/replica", () => Results.Ok(new
    {
        replica = Environment.MachineName
    })).AllowAnonymous();
}

app.MapStaticAssets();
app.MapRazorPages()
    .WithStaticAssets();
app.Run();
