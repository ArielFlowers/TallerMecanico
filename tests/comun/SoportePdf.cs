using System.Diagnostics;
using System.Text;

namespace TallerMecanico.IntegrationTests;

public sealed class PdfContenidoFactAttribute : FactAttribute
{
    public PdfContenidoFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("TALLER_PDF_PYTHON")))
            Skip = "Configura TALLER_PDF_PYTHON con Python y pypdf para verificar el contenido del PDF.";
    }
}

internal static class SoportePdf
{
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
