using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;
using TallerMecanico.Api;
using TallerMecanico.Aplicacion.Configuracion;
using TallerMecanico.Aplicacion.Puertos;
using TallerMecanico.Aplicacion.CasosUso.Ordenes.Contratos;
using TallerMecanico.Infraestructura.Seguridad;
using TallerMecanico.Infraestructura.Sesion;

var builder =
    WebApplication.CreateBuilder(args);

// Las claves persistidas conservan las cookies al reiniciar el Frontend.
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
// pruebas en localhost:80 y localhost:8088.
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
builder.Services.AddScoped<IBorradorOrden, BorradorOrdenSesion>();
builder.Services.AddScoped<ITokenOrden, TokenOrdenSesion>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton(ConfiguracionTaller.Instancia);
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
// CLIENTES HTTP DE LOS SERVICIOS BACKEND
// =====================================================
builder.Services.AddApisInternas(builder.Configuration);

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}
// Kestrel sirve HTTPS directamente. En desarrollo también se admite HTTP local.
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
app.MapGet("/salud", () => Results.Ok()).AllowAnonymous();

app.MapStaticAssets();
app.MapRazorPages()
    .WithStaticAssets();
app.Run();
