using Microsoft.Extensions.DependencyInjection;
using TallerMecanico.Aplicacion.Configuracion;
using TallerMecanico.Aplicacion.Puertos;
using TallerMecanico.Aplicacion.Servicios.Reportes;
using TallerMecanico.Infraestructura.Documentos;
using TallerMecanico.Infraestructura.Exportacion;
using TallerMecanico.Infraestructura.Persistencia;
using TallerMecanico.Infraestructura.Persistencia.MySql;

namespace TallerMecanico.Infraestructura;

public static class RegistroDependencias
{
    public static IServiceCollection AddInfraestructuraReportes(
        this IServiceCollection servicios)
    {
        servicios.AddSingleton(ConfiguracionTaller.Instancia);
        servicios.AddScoped<DatabaseConnectionFactory, MySqlConnectionFactory>();
        servicios.AddScoped<IConsultaReportesPort, ConsultaReportesMySqlAdapter>();
        servicios.AddScoped<IGeneradorReporteOrdenesPdfPort, QuestPdfReporteOrdenesAdapter>();
        servicios.AddScoped<IGeneradorReporteOrdenesExcelPort, ClosedXmlReporteOrdenesAdapter>();
        servicios.AddScoped<IGeneradorReporteMecanicosPdfPort, QuestPdfReporteMecanicosAdapter>();
        servicios.AddScoped<IGeneradorReporteMecanicosExcelPort, ClosedXmlReporteMecanicosAdapter>();
        servicios.AddScoped<IReporteOrdenesPort, ReporteOrdenesService>();
        servicios.AddScoped<IReporteMecanicosPort, ReporteMecanicosService>();
        QuestPDF.Settings.License =
            QuestPDF.Infrastructure.LicenseType.Community;
        return servicios;
    }
}
