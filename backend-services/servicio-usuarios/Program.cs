using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TallerMecanico.Aplicacion.Puertos;
using TallerMecanico.Controllers;
using TallerMecanico.Infraestructura;
using TallerMecanico.Infraestructura.Arranque;
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
        new BadRequestObjectResult(new TallerMecanico.Contratos.ErrorApi("Los datos de la solicitud no son válidos."));
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

// =====================================================
// DEPENDENCIAS DEL SERVICIO
// =====================================================
builder.Services.AddInfraestructuraUsuarios(builder.Configuration);
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
