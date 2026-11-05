
using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using ClosedXML.Excel;
using TallerMecanico.Aplicacion.DTOs.Reportes;
using TallerMecanico.Aplicacion.Configuracion;
using TallerMecanico.Infraestructura.Exportacion;
using Xunit;

namespace TallerMecanico.IntegrationTests.Reportes;
[Trait("Area", "Backend")]
public class ClosedXmlReporteMecanicosAdapterTests
{
    [Fact]
    public void Generar_ProduceXlsxGenuinoConDatos()
    {
        var adapter = new ClosedXmlReporteMecanicosAdapter(
            ConfiguracionTaller.Instancia);

        var documento = new DocumentoReporteMecanicos
        {
            Filtro = new FiltroReporteMecanicos(),
            Filas =
            [
                new FilaReporteMecanico
                {
                    MecanicoId = 1,
                    NombreMecanico = "Carlos Ruiz",
                    CiMecanico = "1234567",
                    TotalOrdenes = 3,
                    TotalFacturado = 600.00m,
                    PromedioPorOrden = 200.00m
                },
                new FilaReporteMecanico
                {
                    MecanicoId = 2,
                    NombreMecanico = "Ana Torres",
                    CiMecanico = "7654321",
                    TotalOrdenes = 2,
                    TotalFacturado = 320.50m,
                    PromedioPorOrden = 160.25m
                }
            ],
            Resumen = new ResumenReporteMecanicos
            {
                TotalMecanicos = 2,
                TotalOrdenes = 5,
                TotalFacturado = 920.50m
            },
            GeneradoPor = "admin",
            FechaGeneracion = new DateTime(2026, 1, 15, 10, 0, 0)
        };

        byte[] xlsx = adapter.Generar(documento);

        Assert.NotEmpty(xlsx);
        Assert.Equal(0x50, xlsx[0]);
        Assert.Equal(0x4B, xlsx[1]);

        using (var archivo = new ZipArchive(
            new MemoryStream(xlsx, writable: false)))
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
            Assert.DoesNotContain(
                "dataBar",
                LeerEntrada(archivo, "xl/worksheets/sheet1.xml"));
        }

        using var libroLeido = new XLWorkbook(
            new MemoryStream(xlsx, writable: false));

        var hoja = libroLeido.Worksheet(1);

        var grafico = Assert.Single(hoja.Pictures, imagen => imagen.Name == "Total facturado por mecánico");
        Assert.True(grafico.ImageStream.Length > 1000);

        Assert.Equal("AutoTaller Pro", hoja.Cell(1, 1).GetString());
        Assert.Equal(
            "Reporte estadístico por mecánico",
            hoja.Cell(2, 1).GetString());

        Assert.Equal("Carlos Ruiz", hoja.Cell(4, 2).GetString());
        Assert.Equal("Ana Torres", hoja.Cell(5, 2).GetString());

        Assert.Equal("1234567", hoja.Cell(4, 3).GetString());
        Assert.Equal("7654321", hoja.Cell(5, 3).GetString());

        Assert.Equal(3, hoja.Cell(4, 4).GetValue<int>());
        Assert.Equal(2, hoja.Cell(5, 4).GetValue<int>());

        Assert.Equal(600.00m, hoja.Cell(4, 5).GetValue<decimal>());
        Assert.Equal(320.50m, hoja.Cell(5, 5).GetValue<decimal>());

        Assert.Equal(200.00m, hoja.Cell(4, 6).GetValue<decimal>());
        Assert.Equal(160.25m, hoja.Cell(5, 6).GetValue<decimal>());

        Assert.Contains(
            920.50m.ToString("N2", CultureInfo.CurrentCulture),
            hoja.Cell(9, 2).GetString());
    }

    [Fact]
    public void Generar_ConDatasetVacio_ProduceXlsxGenuino()
    {
        var adapter = new ClosedXmlReporteMecanicosAdapter(
            ConfiguracionTaller.Instancia);

        var documento = new DocumentoReporteMecanicos
        {
            Filtro = new FiltroReporteMecanicos(),
            Filas = [],
            Resumen = new ResumenReporteMecanicos(),
            GeneradoPor = "admin",
            FechaGeneracion = DateTime.UtcNow
        };

        byte[] xlsx = adapter.Generar(documento);

        Assert.NotEmpty(xlsx);
        Assert.Equal(0x50, xlsx[0]);
        Assert.Equal(0x4B, xlsx[1]);

        using (var archivo = new ZipArchive(
            new MemoryStream(xlsx, writable: false)))
        {
            Assert.Contains(
                "xl/worksheets/sheet1.xml",
                archivo.Entries.Select(e => e.FullName));
        }

        using var libroLeido = new XLWorkbook(
            new MemoryStream(xlsx, writable: false));

        var hoja = libroLeido.Worksheet(1);

        Assert.Equal("AutoTaller Pro", hoja.Cell(1, 1).GetString());

        Assert.Equal(
            "Reporte estadístico por mecánico",
            hoja.Cell(2, 1).GetString());
    }

    [Fact]
    public void Generar_ValidaEstructuraOpenXmlYTiposDeCelda()
    {
        var adapter = new ClosedXmlReporteMecanicosAdapter(
            ConfiguracionTaller.Instancia);

        var documento = new DocumentoReporteMecanicos
        {
            Filtro = new FiltroReporteMecanicos(),
            Filas =
            [
                new FilaReporteMecanico
                {
                    MecanicoId = 1,
                    NombreMecanico = "Mecánico Uno",
                    CiMecanico = "CI-001",
                    TotalOrdenes = 1,
                    TotalFacturado = 150.00m,
                    PromedioPorOrden = 150.00m
                }
            ],
            Resumen = new ResumenReporteMecanicos
            {
                TotalMecanicos = 1,
                TotalOrdenes = 1,
                TotalFacturado = 150.00m
            },
            GeneradoPor = "admin",
            FechaGeneracion = new DateTime(2026, 1, 15, 10, 0, 0)
        };

        byte[] xlsx = adapter.Generar(documento);

        using var archivo = new ZipArchive(
            new MemoryStream(xlsx, writable: false));

        // Validar el libro y la relación con su hoja de cálculo.
        string workbook = LeerEntrada(archivo, "xl/workbook.xml");
        string relaciones = LeerEntrada(
            archivo,
            "xl/_rels/workbook.xml.rels");

        XNamespace nsSpreadsheet =
            "http://schemas.openxmlformats.org/spreadsheetml/2006/main";

        XNamespace nsOfficeRelationships =
            "http://schemas.openxmlformats.org/officeDocument/2006/relationships";

        XNamespace nsPackageRelationships =
            "http://schemas.openxmlformats.org/package/2006/relationships";

        XDocument documentoWorkbook = XDocument.Parse(workbook);

        var elementoHoja = documentoWorkbook
            .Descendants(nsSpreadsheet + "sheet")
            .SingleOrDefault();

        Assert.NotNull(elementoHoja);

        Assert.Equal(
            "Mecánicos",
            (string?)elementoHoja!.Attribute("name"));

        Assert.Equal(
            "1",
            (string?)elementoHoja.Attribute("sheetId"));

        string? relationshipId = (string?)elementoHoja.Attribute(
            nsOfficeRelationships + "id");

        Assert.False(string.IsNullOrWhiteSpace(relationshipId));

        XDocument documentoRelaciones = XDocument.Parse(relaciones);

        var relacionHoja = documentoRelaciones
            .Descendants(nsPackageRelationships + "Relationship")
            .SingleOrDefault(r =>
                (string?)r.Attribute("Id") == relationshipId);

        Assert.NotNull(relacionHoja);

        string tipoRelacion =
            (string?)relacionHoja!.Attribute("Type") ?? "";

        string destinoRelacion =
            (string?)relacionHoja.Attribute("Target") ?? "";

        Assert.EndsWith("/worksheet",tipoRelacion,StringComparison.OrdinalIgnoreCase);

        Assert.EndsWith("worksheets/sheet1.xml",destinoRelacion.Replace('\\', '/'),StringComparison.OrdinalIgnoreCase);

        // Validar la configuración visual de la hoja.
        string contenidoHoja = LeerEntrada(
            archivo,
            "xl/worksheets/sheet1.xml");

        XDocument documentoHoja = XDocument.Parse(contenidoHoja);

        var sheetView = documentoHoja
            .Descendants(nsSpreadsheet + "sheetView")
            .SingleOrDefault();

        Assert.NotNull(sheetView);

        string? valorGridLines = (string?)sheetView!.Attribute("showGridLines");

        Assert.True(valorGridLines is "0" or "false");

        // Validar que la celda E4 contiene un número real.
        var celdaNumerica = documentoHoja
            .Descendants(nsSpreadsheet + "c")
            .SingleOrDefault(c =>
                (string?)c.Attribute("r") == "E4");

        Assert.NotNull(celdaNumerica);

        string? tipoCelda = (string?)celdaNumerica!.Attribute("t");

        Assert.True(
            string.IsNullOrEmpty(tipoCelda) || tipoCelda == "n");

        string? valorCelda =
            (string?)celdaNumerica.Element(nsSpreadsheet + "v");

        Assert.True(decimal.TryParse(
            valorCelda,
            NumberStyles.Number,
            CultureInfo.InvariantCulture,
            out decimal valorNumerico));

        Assert.Equal(150.00m, valorNumerico);

        // Comprobar también los valores interpretados por ClosedXML.
        using var libroLeido = new XLWorkbook(
            new MemoryStream(xlsx, writable: false));

        var hoja = libroLeido.Worksheet(1);

        Assert.Equal(
            XLDataType.Number,
            hoja.Cell("E4").DataType);

        Assert.Equal(
            150.00m,
            hoja.Cell("E4").GetValue<decimal>());

        Assert.Equal(
            "Mecánico Uno",
            hoja.Cell("B4").GetString());
    }

    private static string LeerEntrada(
        ZipArchive archivo,
        string nombre)
    {
        var entrada = archivo.GetEntry(nombre);

        Assert.NotNull(entrada);

        using var reader = new StreamReader(
            entrada!.Open(),
            Encoding.UTF8);

        return reader.ReadToEnd();
    }
}
