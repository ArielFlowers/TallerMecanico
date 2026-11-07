using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using TallerMecanico.Aplicacion.Puertos;
using TallerMecanico.Controllers;
using TallerMecanico.Infraestructura;
using TallerMecanico.Infraestructura.Seguridad;

// Servicio 3: reportes (Hexagonal).
var builder = WebApplication.CreateBuilder(args);

// Las reglas de cada campo las valida la aplicación, no el enlace de MVC.
builder.Services.AddControllers(opciones =>
{
    opciones.Filters.Add<ErroresApiFilter>();
    opciones.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true;
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
// DEPENDENCIAS DEL SERVICIO (las agrega el dueño del servicio)
// =====================================================
builder.Services.AddInfraestructuraReportes();

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/salud", () => Results.Ok()).AllowAnonymous();
app.MapControllers();
app.Run();
