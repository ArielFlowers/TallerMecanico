using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TallerMecanico.Aplicacion.Configuracion;
using TallerMecanico.Aplicacion.Puertos;
using TallerMecanico.Aplicacion.Servicios.Reportes;
using TallerMecanico.Contratos;
using TallerMecanico.Controllers;
using TallerMecanico.Infraestructura.Documentos;
using TallerMecanico.Infraestructura.Exportacion;
using TallerMecanico.Infraestructura.Persistencia;
using TallerMecanico.Infraestructura.Persistencia.MySql;
using TallerMecanico.Infraestructura.Seguridad;

// Servicio 3: reportes (Hexagonal).
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

// =====================================================
// ADAPTADORES DE SALIDA: CONSULTA MYSQL, PDF Y EXCEL
// =====================================================
builder.Services.AddScoped<IConsultaReportesPort, ConsultaReportesMySqlAdapter>();
builder.Services.AddScoped<IGeneradorReporteOrdenesPdfPort, QuestPdfReporteOrdenesAdapter>();
builder.Services.AddScoped<IGeneradorReporteOrdenesExcelPort, ClosedXmlReporteOrdenesAdapter>();
builder.Services.AddScoped<IGeneradorReporteMecanicosPdfPort, QuestPdfReporteMecanicosAdapter>();
builder.Services.AddScoped<IGeneradorReporteMecanicosExcelPort, ClosedXmlReporteMecanicosAdapter>();
QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

// =====================================================
// PUERTOS DE ENTRADA: CASOS DE USO
// =====================================================
builder.Services.AddScoped<IReporteOrdenesPort, ReporteOrdenesService>();
builder.Services.AddScoped<IReporteMecanicosPort, ReporteMecanicosService>();

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/salud", () => Results.Ok()).AllowAnonymous();
app.MapControllers();
app.Run();
