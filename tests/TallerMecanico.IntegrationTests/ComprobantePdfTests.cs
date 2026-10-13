using System.Text;
using TallerMecanico.Aplicacion.CasosUso.Ordenes;
using TallerMecanico.Dominio.Entidades;
using TallerMecanico.Aplicacion.Servicios;

namespace TallerMecanico.IntegrationTests;

public sealed class ComprobantePdfTests
{
    private static readonly DateTimeOffset FechaGeneracion = new(2026, 10, 12, 20, 30, 0, TimeSpan.Zero);

    [Fact]
    public void Generacion_ProduceArchivoPdfReal()
    {
        byte[] pdf = Generar(SoporteComprobantePdf.Orden());
        Assert.StartsWith("%PDF-", Encoding.ASCII.GetString(pdf, 0, 8));
        Assert.Contains("%%EOF", Encoding.ASCII.GetString(pdf.AsSpan(Math.Max(0, pdf.Length - 30))));
        Assert.True(pdf.Length > 1000);
    }

    [PdfContenidoFact]
    public void Contenido_ConservaDatosImportesLiteralesYFechasEnBolivia()
    {
        byte[] pdf = Generar(SoporteComprobantePdf.Orden());
        string texto = SoporteComprobantePdf.ExtraerTexto(pdf);
        foreach (string esperado in new[] { "AutoTaller Pro", "COMPROBANTE DE VENTA", "1234ABC", "Toyota", "Corolla",
            "María Fernanda Núñez", "1234567-1A", "71234567", "José Muñoz Pérez", "Aceite sintético", "Filtro de transmisión",
            "20,00", "11,50", "31,50", "Son Treinta y Un Bolivianos con 50/100.", "creador_original", "generador_copia",
            "12/10/2026 10:00", "12/10/2026 16:30:00", "UTC-4" })
            Assert.Contains(esperado, texto);
        Assert.Single(texto.Split('\f', StringSplitOptions.RemoveEmptyEntries));
        SoporteComprobantePdf.GuardarMuestra(pdf, "comprobante-activa.pdf");
    }

    [PdfContenidoFact]
    public void AnuladaYDocumentoExtenso_NoPierdenProductosYRepitenCabecera()
    {
        var orden = SoporteComprobantePdf.Orden() with
        {
            Estado = EstadoOrden.Anulada, AnuladoPor = "admin_anulador",
            FechaAnulacion = new DateTime(2026, 10, 13, 2, 0, 0, DateTimeKind.Utc),
            Productos = Enumerable.Range(1, 120).Select(numero =>
                new ProductoOrdenDetalle(numero.ToString(), $"Producto español {numero:D3}: diagnóstico de transmisión y revisión", 1, 10m, 10m)).ToArray()
        };
        byte[] pdf = Generar(orden);
        string texto = SoporteComprobantePdf.ExtraerTexto(pdf);
        string[] paginas = texto.Split('\f', StringSplitOptions.RemoveEmptyEntries);
        Assert.True(paginas.Length > 1);
        Assert.All(paginas, pagina => { Assert.Contains("ANULADA", pagina); Assert.Contains("COMPROBANTE DE VENTA", pagina); });
        for (int numero = 1; numero <= 120; numero++) Assert.Contains($"Producto español {numero:D3}", texto);
        Assert.Contains("admin_anulador", texto);
        Assert.Contains("12/10/2026 22:00", texto);
        Assert.Contains("TOTAL: Bs. 31,50", texto);
        SoporteComprobantePdf.GuardarMuestra(pdf, "comprobante-anulada-extenso.pdf");
    }

    [Fact]
    public void ImporteMaximo_NoRompeLaComposicion()
    {
        var orden = SoporteComprobantePdf.Orden() with
        {
            Total = 9999999999999999.99m,
            Productos = [new("MAX", "Importe máximo del esquema", 1, 99999999.99m, 9999999999999999.99m)]
        };
        Assert.True(Generar(orden).Length > 1000);
    }

    private static byte[] Generar(OrdenDetalle orden) => SoporteComprobantePdf.CrearGenerador().Generar(
        orden, new ImporteEnPalabras().Convertir(orden.Total), "generador_copia", FechaGeneracion);
}
