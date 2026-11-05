namespace TallerMecanico.Infraestructura.Documentos;

// El mismo logo para el comprobante y los reportes.
internal static class LogoTaller
{
    private const string Recurso = "TallerMecanico.Infraestructura.Exportacion.logo-autotaller.png";

    public static readonly byte[] Imagen = Leer();

    private static byte[] Leer()
    {
        using Stream recurso = typeof(LogoTaller).Assembly.GetManifestResourceStream(Recurso)
            ?? throw new InvalidOperationException("No se encontró el logo del taller.");
        using var memoria = new MemoryStream();
        recurso.CopyTo(memoria);
        return memoria.ToArray();
    }
}
