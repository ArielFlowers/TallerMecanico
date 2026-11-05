
using System.IO.Compression;
using ClosedXML.Excel;
using TallerMecanico.Aplicacion.DTOs.Reportes;
using TallerMecanico.Aplicacion.Configuracion;
using TallerMecanico.Infraestructura.Exportacion;
using Xunit;

namespace TallerMecanico.IntegrationTests.Reportes;
[Trait("Area", "Backend")]
public class ClosedXmlReporteOrdenesAdapterTests
{
    [Fact]
    public void Generar_ProduceXlsxGenuinoConDatos()
    {
        var adapter = new ClosedXmlReporteOrdenesAdapter(
            ConfiguracionTaller.Instancia);

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

        byte[] xlsx = adapter.Generar(documento);

        // Comprobar que se genera un archivo XLSX genuino.
        Assert.NotEmpty(xlsx);
        Assert.Equal(0x50, xlsx[0]);
        Assert.Equal(0x4B, xlsx[1]);

        using (var archivo = new ZipArchive(
            new MemoryStream(xlsx, writable: false),
            ZipArchiveMode.Read))
        {
            var nombres = archivo.Entries
                .Select(e => e.FullName)
                .ToHashSet();

            Assert.Contains("[Content_Types].xml", nombres);
            Assert.Contains("_rels/.rels", nombres);
            Assert.Contains("xl/workbook.xml", nombres);
            Assert.Contains("xl/styles.xml", nombres);
            Assert.Contains("xl/worksheets/sheet1.xml", nombres);
            Assert.Contains(nombres, n => n.StartsWith("xl/media/", StringComparison.Ordinal));
        }

        // Comprobar el contenido real del Excel mediante ClosedXML.
        using var libroLeido = new XLWorkbook(
            new MemoryStream(xlsx, writable: false));

        var hoja = libroLeido.Worksheet(1);

        Assert.Equal("AutoTaller Pro", hoja.Cell(1, 1).GetString());

        Assert.StartsWith(
            "Reporte detallado",
            hoja.Cell(2, 1).GetString());

        // Comprobar los datos de la orden.
        Assert.Equal(7, hoja.Cell(4, 1).GetValue<int>());

        Assert.Equal(
            new DateTime(2026, 1, 15, 10, 0, 0),
            hoja.Cell(4, 2).GetValue<DateTime>());

        Assert.Equal("ABC-123", hoja.Cell(4, 3).GetString());
        Assert.Equal("Cliente Prueba", hoja.Cell(4, 4).GetString());
        Assert.Equal("Activa", hoja.Cell(4, 8).GetString());

        Assert.Equal(
            100.00m,
            hoja.Cell(4, 7).GetValue<decimal>());
    }

    [Fact]
    public void Generar_ConDatasetVacio_ProduceXlsxGenuino()
    {
        var adapter = new ClosedXmlReporteOrdenesAdapter(
            ConfiguracionTaller.Instancia);

        var documento = new DocumentoReporteOrdenes
        {
            Filtro = new FiltroReporteOrdenes(),
            Filas = [],
            Resumen = new ResumenReporteOrdenes(),
            GeneradoPor = "admin",
            FechaGeneracion = DateTime.UtcNow
        };

        byte[] xlsx = adapter.Generar(documento);

        Assert.NotEmpty(xlsx);
        Assert.Equal(0x50, xlsx[0]);
        Assert.Equal(0x4B, xlsx[1]);

        using (var archivo = new ZipArchive(
            new MemoryStream(xlsx, writable: false),
            ZipArchiveMode.Read))
        {
            Assert.Contains(
                "xl/worksheets/sheet1.xml",
                archivo.Entries.Select(e => e.FullName));
        }

        // Verificar que el documento vacío también conserva
        // la estructura y los títulos del reporte.
        using var libroLeido = new XLWorkbook(
            new MemoryStream(xlsx, writable: false));

        var hoja = libroLeido.Worksheet(1);

        Assert.Equal("AutoTaller Pro", hoja.Cell(1, 1).GetString());

        Assert.StartsWith(
            "Reporte detallado",
            hoja.Cell(2, 1).GetString());
    }
}
