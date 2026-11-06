using Microsoft.Extensions.DependencyInjection;
using TallerMecanico.Aplicacion.CasosUso.Ordenes;
using TallerMecanico.Aplicacion.CasosUso.Ordenes.Contratos;
using TallerMecanico.Aplicacion.Fachadas;
using TallerMecanico.Aplicacion.Validaciones;
using TallerMecanico.Dominio.Servicios;
using TallerMecanico.Infraestructura.Documentos;
using TallerMecanico.Infraestructura.Persistencia.Fabricas;
using TallerMecanico.Infraestructura.Persistencia.MySql;

namespace TallerMecanico.Infraestructura;

public static class RegistroDependencias
{
    public static IServiceCollection AddInfraestructuraOrdenes(
        this IServiceCollection servicios)
    {
        servicios.AddScoped<IUnidadTrabajoOrden, MySqlUnidadTrabajoOrden>();
        servicios.AddScoped<ValidacionOrdenes>();
        servicios.AddScoped<CrearOrdenCasoUso>();
        servicios.AddScoped<AnularOrdenCasoUso>();
        servicios.AddScoped<OrdenServicioFacade>();
        servicios.AddScoped<CreadorConsultaOrdenes>();
        servicios.AddScoped<IOrdenQueryRepository>(proveedor =>
            proveedor
                .GetRequiredService<CreadorConsultaOrdenes>()
                .CrearRepositorio());
        servicios.AddScoped<ConsultarOrdenesCasoUso>();
        servicios.AddScoped<ConsultaOrdenesFacade>();
        servicios.AddSingleton<ImporteEnPalabras>();
        servicios.AddScoped<IComprobantePdf, ComprobanteOrdenPdf>();
        servicios.AddScoped<GenerarComprobanteCasoUso>();
        QuestPDF.Settings.License =
            QuestPDF.Infrastructure.LicenseType.Community;
        return servicios;
    }
}
