extern alias Ordenes;

using TallerMecanico.Aplicacion.Fachadas;
using TallerMecanico.Aplicacion.CasosUso.Ordenes;
using TallerMecanico.Aplicacion.CasosUso.Ordenes.Contratos;
using TallerMecanico.Aplicacion.Puertos;
using TallerMecanico.Infraestructura.Documentos;
using TallerMecanico.Dominio.Entidades;
using TallerMecanico.Dominio.Servicios;
using TallerMecanico.Aplicacion.Servicios;
using ConfiguracionTaller = Ordenes::TallerMecanico.Aplicacion.Configuracion.ConfiguracionTaller;

namespace TallerMecanico.IntegrationTests;

internal static class SoporteComprobantePdf
{
    public static ConsultaOrdenesFacade CrearFachada(ConsultarOrdenesCasoUso consultas,
        ICurrentUser? usuario = null, IComprobantePdf? generador = null, TimeProvider? reloj = null) =>
        new(consultas, new GenerarComprobanteCasoUso(consultas, generador ?? CrearGenerador(),
            new ImporteEnPalabras(), usuario ?? new UsuarioPrueba(), reloj ?? TimeProvider.System));

    public static ComprobanteOrdenPdf CrearGenerador()
    {
        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
        return new ComprobanteOrdenPdf(ConfiguracionTaller.Instancia);
    }

    public static OrdenDetalle Orden() => new()
    {
        Id = 15, Fecha = new DateTime(2026, 10, 12), Estado = EstadoOrden.Activa, Total = 31.50m,
        Placa = "1234ABC", Marca = "Toyota", Modelo = "Corolla", NombreCliente = "María Fernanda Núñez",
        CiCliente = "1234567", ComplementoCiCliente = "1A", CelularCliente = "71234567",
        NombreMecanico = "José Muñoz Pérez", CreadoPor = "creador_original",
        FechaCreacion = new DateTime(2026, 10, 12, 14, 0, 0, DateTimeKind.Utc),
        Productos = [new("ACE", "Aceite sintético", 2, 10m, 20m), new("FIL", "Filtro de transmisión", 1, 11.50m, 11.50m)]
    };
}
