using AuthenticationSchemeOptions = Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TallerMecanico.Aplicacion.Fachadas;
using TallerMecanico.Aplicacion.Puertos;
using TallerMecanico.Aplicacion.Servicios;
using TallerMecanico.Contratos;
using TallerMecanico.Controllers;
using TallerMecanico.Infraestructura.Arranque;
using TallerMecanico.Infraestructura.Correo;
using TallerMecanico.Infraestructura.Persistencia;
using TallerMecanico.Infraestructura.Persistencia.MySql;
using TallerMecanico.Infraestructura.Seguridad;

// Servicio 2: usuarios y autenticación (Hexagonal).
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
builder.Services.AddScoped<DatabaseConnectionFactory, MySqlConnectionFactory>();
builder.Services.AddScoped<IAuditoriaPort, AuditoriaRepository>();

// =====================================================
// ADAPTADORES DE SALIDA: MYSQL, CORREO Y CONTRASEÑAS
// =====================================================
builder.Services.AddScoped<UsuarioRepository>();
builder.Services.AddScoped<IUsuarioPort>(proveedor => proveedor.GetRequiredService<UsuarioRepository>());
builder.Services.AddScoped<IUsuarioConsultaPort>(proveedor => proveedor.GetRequiredService<UsuarioRepository>());
builder.Services.AddScoped<IUsuarioAdministracionPort>(proveedor => proveedor.GetRequiredService<UsuarioRepository>());
builder.Services.AddScoped<ISolicitudRegistroPort, SolicitudRegistroRepository>();
builder.Services.AddScoped<IActivacionInvitacionPort, ActivacionInvitacionMySqlAdapter>();
builder.Services.AddScoped<IRevisionSolicitudesPort, RevisionSolicitudesRepository>();
builder.Services.AddScoped<IEmailService, GmailEmailService>();
builder.Services.Configure<Argon2idOptions>(builder.Configuration.GetSection(Argon2idOptions.SectionName));
builder.Services.AddSingleton<IPasswordHasher, Argon2idPasswordHasher>();
builder.Services.AddSingleton<IFallbackHash, AuthenticationFallbackHash>();

// =====================================================
// PUERTOS DE ENTRADA: CASOS DE USO
// =====================================================
builder.Services.AddScoped<IAuthenticationService, AuthenticationService>();
builder.Services.AddScoped<IGeneradorNombreUsuarioPort, GeneradorNombreUsuarioService>();
builder.Services.AddScoped<IGestionUsuariosPort, UsuarioAdministracionService>();
builder.Services.AddScoped<ICambioContrasenaPort, CambioContrasenaService>();
builder.Services.AddScoped<RegistroUsuarioService>();
builder.Services.AddScoped<RegistroFacade>();
builder.Services.AddScoped<VerificacionEmailService>();
builder.Services.AddScoped<ReenvioVerificacionService>();
builder.Services.AddScoped<RevisionSolicitudesService>();

// =====================================================
// COMANDOS DE CONSOLA
// =====================================================
builder.Services.AddScoped<InicializadorUsuarios>();
builder.Services.AddScoped<InicializacionUsuariosConsola>();
builder.Services.AddScoped<RestablecimientoAdminConsola>();

var app = builder.Build();

// El esquema lo administra Servicio 1; estos comandos solo gestionan cuentas.
if (args.Contains("--inicializar-usuarios", StringComparer.OrdinalIgnoreCase) ||
    args.Contains("--restablecer-admin", StringComparer.OrdinalIgnoreCase))
{
    if (args.Length != 1)
        throw new InvalidOperationException("El comando administrativo no admite argumentos adicionales.");
    if (!app.Environment.IsDevelopment())
        throw new InvalidOperationException("Los comandos administrativos solo están permitidos en Development.");
}
if (args.Contains("--inicializar-usuarios", StringComparer.OrdinalIgnoreCase))
{
    using var alcance = app.Services.CreateScope();
    alcance.ServiceProvider.GetRequiredService<InicializacionUsuariosConsola>().Ejecutar();
    return;
}
if (args.Contains("--restablecer-admin", StringComparer.OrdinalIgnoreCase))
{
    using var alcance = app.Services.CreateScope();
    alcance.ServiceProvider.GetRequiredService<RestablecimientoAdminConsola>().Ejecutar();
    return;
}

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/salud", () => Results.Ok()).AllowAnonymous();
app.MapControllers();
app.Run();
