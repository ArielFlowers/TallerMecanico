using TallerMecanico.Aplicacion.Configuracion;
using TallerMecanico.Infraestructura.Documentos;
using Xunit;

namespace TallerMecanico.IntegrationTests.Reportes;
[Trait("Area", "Backend")]
public class QuestPdfReporteOrdenesAdapterTests
{
    static QuestPdfReporteOrdenesAdapterTests()
    {
        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Evaluation;
    }

    [Fact]
    public void Generar_ProducePdfValidoConDatos()
    {
        var adapter = new QuestPdfReporteOrdenesAdapter(ConfiguracionTaller.Instancia);
        var documento = new DocumentoReporteOrdenes
        {
            Filtro = new FiltroReporteOrdenes(),
            Filas =
            [
                new FilaReporteOrden
                {
                    OrdenId = 7,
                    Fecha = new DateTime(2026, 1, 15, 10, 0, 0),
                    Placa = "ABC-123",
                    NombreCliente = "Cliente Prueba",
                    NombreMecanico = "Mecánico Prueba",
                    NombreServicio = "Revisión",
                    Total = 100.00m,
                    Estado = "Activa",
                    CreadoPor = "admin"
                }
            ],
            Resumen = new ResumenReporteOrdenes
            {
                TotalOrdenes = 1,
                TotalGeneral = 100.00m,
                OrdenesActivas = 1,
                OrdenesAnuladas = 0
            },
            GeneradoPor = "admin",
            FechaGeneracion = new DateTime(2026, 1, 15, 10, 0, 0)
        };

        byte[] pdf = adapter.Generar(documento);

        Assert.NotEmpty(pdf);
        Assert.StartsWith("%PDF", System.Text.Encoding.UTF8.GetString(pdf, 0, 5));
    }

    [Fact]
    public void Generar_ConDatasetVacio_ProducePdfValido()
    {
        var adapter = new QuestPdfReporteOrdenesAdapter(ConfiguracionTaller.Instancia);
        var documento = new DocumentoReporteOrdenes
        {
            Filtro = new FiltroReporteOrdenes(),
            Filas = [],
            Resumen = new ResumenReporteOrdenes(),
            GeneradoPor = "admin",
            FechaGeneracion = DateTime.UtcNow
        };

        byte[] pdf = adapter.Generar(documento);

        Assert.NotEmpty(pdf);
        Assert.StartsWith("%PDF", System.Text.Encoding.UTF8.GetString(pdf, 0, 5));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(3, 0)]
    [InlineData(0, 2)]
    [InlineData(70, 30)]
    public void Generar_GraficoSoportaSinDatosUnSoloEstadoYMultiplesPaginas(int activas, int anuladas)
    {
        var documento = CrearDocumento(activas, anuladas);
        byte[] pdf = new QuestPdfReporteOrdenesAdapter(ConfiguracionTaller.Instancia).Generar(documento);

        Assert.StartsWith("%PDF", System.Text.Encoding.ASCII.GetString(pdf, 0, 5));
        SoportePdf.GuardarMuestra(pdf, $"reporte-ordenes-{activas}-{anuladas}.pdf");
    }

    [PdfContenidoFact]
    public void Generar_GraficoIncluyeEstadosCantidadesYPorcentajesDelFiltro()
    {
        var adaptador = new QuestPdfReporteOrdenesAdapter(ConfiguracionTaller.Instancia);
        string contenido = SoportePdf.ExtraerTexto(adaptador.Generar(CrearDocumento(3, 1)));

        Assert.Contains("Órdenes por estado", contenido);
        Assert.Contains("Cantidad de órdenes del resultado filtrado", contenido);
        Assert.Contains("3 (75%)", contenido);
        Assert.Contains("1 (25%)", contenido);
        Assert.Contains("Verde: activas", contenido);
        Assert.Contains("Coral: anuladas", contenido);

        string vacio = SoportePdf.ExtraerTexto(adaptador.Generar(CrearDocumento(0, 0)));
        Assert.Contains("Sin órdenes para los filtros seleccionados.", vacio);
        Assert.Contains("0 (0%)", vacio);
    }

    private static DocumentoReporteOrdenes CrearDocumento(int activas, int anuladas) => new()
    {
        Filtro = new FiltroReporteOrdenes(),
        Filas = Enumerable.Range(1, activas + anuladas).Select(id => new FilaReporteOrden
        {
            OrdenId = id,
            Fecha = new DateTime(2026, 11, 5),
            Placa = $"{id:0000}ABC",
            NombreCliente = "Cliente Prueba",
            NombreMecanico = "Mecánico Prueba",
            NombreServicio = "Revisión",
            Total = 100m,
            Estado = id <= activas ? "Activa" : "Anulada",
            CreadoPor = "admin"
        }).ToList(),
        Resumen = new ResumenReporteOrdenes
        {
            TotalOrdenes = activas + anuladas,
            OrdenesActivas = activas,
            OrdenesAnuladas = anuladas,
            TotalGeneral = activas * 100m
        },
        GeneradoPor = "admin",
        FechaGeneracion = DateTime.UtcNow
    };
}
