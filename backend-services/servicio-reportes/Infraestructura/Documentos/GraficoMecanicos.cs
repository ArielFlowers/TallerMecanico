using System.Globalization;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace TallerMecanico.Infraestructura.Documentos;

// Una composición para que las exportaciones PDF y Excel muestren los mismos datos.
internal static class GraficoMecanicos
{
    public const string Titulo = "Total facturado por mecánico";
    private const float AnchoImagen = 520;
    private static readonly CultureInfo Cultura = CultureInfo.GetCultureInfo("es-BO");

    public static void Componer(IContainer contenedor, IReadOnlyList<FilaReporteMecanico> filas, string moneda)
    {
        decimal maximo = Math.Max(1m, filas.Max(fila => fila.TotalFacturado));

        contenedor.Border(1).BorderColor("#e5dfee").Padding(12).Column(columna =>
        {
            columna.Spacing(9);
            columna.Item().Text(Titulo).FontSize(12).Bold().FontColor("#6d28d9");

            foreach (FilaReporteMecanico fila in filas)
                ComponerBarra(columna.Item(), fila, maximo, moneda);
        });
    }

    private static void ComponerBarra(IContainer contenedor, FilaReporteMecanico fila, decimal maximo, string moneda)
    {
        contenedor.Row(renglon =>
        {
            renglon.ConstantItem(130).AlignMiddle().Text(fila.NombreMecanico).FontSize(8).ClampLines(1);
            renglon.RelativeItem().AlignMiddle().Row(barra =>
            {
                if (fila.TotalFacturado > 0)
                    barra.RelativeItem((float)fila.TotalFacturado).Height(14).Background("#7c3aed");
                if (fila.TotalFacturado < maximo)
                    barra.RelativeItem((float)(maximo - fila.TotalFacturado)).Height(14).Background("#ede9f2");
            });
            renglon.ConstantItem(90).AlignRight().AlignMiddle()
                .Text($"{moneda} {fila.TotalFacturado.ToString("N2", Cultura)}").FontSize(8).FontColor("#251e35");
        });
    }

    public static float AltoImagen(int cantidadFilas) => 80 + cantidadFilas * 24;

    public static byte[] GenerarImagen(IReadOnlyList<FilaReporteMecanico> filas, string moneda) =>
        Document.Create(documento => documento.Page(pagina =>
        {
            pagina.Size(AnchoImagen, AltoImagen(filas.Count));
            pagina.Margin(8);
            pagina.DefaultTextStyle(estilo => estilo.FontFamily("Lato"));
            pagina.Content().Element(contenedor => Componer(contenedor, filas, moneda));
        })).GenerateImages(new ImageGenerationSettings { RasterDpi = 144 }).Single();
}
