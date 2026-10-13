using System.Diagnostics;
using System.Text;
using TallerMecanico.Application;
using TallerMecanico.Application.Ordenes;
using TallerMecanico.Application.Ports;
using TallerMecanico.Infraestructura.Pdf;
using TallerMecanico.Models;
using TallerMecanico.Services;

namespace TallerMecanico.IntegrationTests;

public sealed class PdfContenidoFactAttribute : FactAttribute
{
    public PdfContenidoFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("TALLER_PDF_PYTHON")))
            Skip = "Configura TALLER_PDF_PYTHON con Python y pypdf para verificar el contenido del PDF.";
    }
}

internal static class SoporteComprobantePdf
{
    public static ConsultaOrdenesFacade CrearFachada(ConsultaOrdenesService consultas,
        ICurrentUser? usuario = null, IComprobanteOrdenPdf? generador = null, TimeProvider? reloj = null) =>
        new(consultas, new ComprobanteOrdenService(consultas, generador ?? CrearGenerador(),
            new ImporteEnPalabras(), usuario ?? new UsuarioPrueba(), reloj ?? TimeProvider.System));

    public static ComprobanteOrdenPdf CrearGenerador()
    {
        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
        return new ComprobanteOrdenPdf();
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

    public static string ExtraerTexto(byte[] pdf)
    {
        string ruta = Path.Combine(Path.GetTempPath(), $"autotaller_pdf_{Guid.NewGuid():N}.pdf");
        File.WriteAllBytes(ruta, pdf);
        try
        {
            var inicio = new ProcessStartInfo(Environment.GetEnvironmentVariable("TALLER_PDF_PYTHON")!)
            {
                UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardOutput = true, RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8
            };
            inicio.ArgumentList.Add(Path.Combine(MySqlAisladoFixture.RaizProyecto, "tests/pdf_text.py"));
            inicio.ArgumentList.Add(ruta);
            using var proceso = Process.Start(inicio)!;
            string texto = proceso.StandardOutput.ReadToEnd();
            string errores = proceso.StandardError.ReadToEnd();
            proceso.WaitForExit();
            Assert.Equal(0, proceso.ExitCode);
            Assert.True(string.IsNullOrWhiteSpace(errores), errores);
            return texto;
        }
        finally { File.Delete(ruta); }
    }

    public static void GuardarMuestra(byte[] pdf, string nombre)
    {
        string? directorio = Environment.GetEnvironmentVariable("TALLER_PDF_MUESTRAS");
        if (directorio is null) return;
        Directory.CreateDirectory(directorio);
        File.WriteAllBytes(Path.Combine(directorio, nombre), pdf);
    }
}
