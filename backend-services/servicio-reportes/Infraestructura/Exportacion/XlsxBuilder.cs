using System.Globalization;
using ClosedXML.Excel;

namespace TallerMecanico.Infraestructura.Exportacion;

internal static class XlsxBuilder
{
    private const string RecursoLogo =
        "TallerMecanico.Infraestructura.Exportacion.logo-autotaller.png";

    public static byte[] Build(
        string nombreHoja,
        string tituloReporte,
        IReadOnlyList<string> cabeceras,
        IReadOnlyList<IReadOnlyList<object?>> filas,
        IReadOnlyList<XlsxFilaResumen>? resumen = null,
        IReadOnlyList<int>? columnasMoneda = null,
        IReadOnlyList<int>? columnasFecha = null,
        byte[]? grafico = null,
        string tituloGrafico = "Gráfico",
        int altoGrafico = 250)
    {
        using var libro = new XLWorkbook();
        var hoja = libro.Worksheets.Add(nombreHoja);

        hoja.ShowGridLines = false;

        int columnas = cabeceras.Count;
        var monedas = columnasMoneda?.ToHashSet() ?? new HashSet<int>();
        var fechas = columnasFecha?.ToHashSet() ?? new HashSet<int>();

        // Titulo principal
        hoja.Cell(1, 1).Value = "AutoTaller Pro";
        hoja.Range(1, 1, 1, columnas).Merge();

        var titulo = hoja.Range(1, 1, 1, columnas);
        titulo.Style.Fill.BackgroundColor = XLColor.FromHtml("#241435");
        titulo.Style.Font.FontColor = XLColor.White;
        titulo.Style.Font.Bold = true;
        titulo.Style.Font.FontSize = 18;

        hoja.Row(1).Height = 38;

        // Logo institucional (monograma AP mientras no exista el logo oficial).
        using (Stream? logo = typeof(XlsxBuilder).Assembly
            .GetManifestResourceStream(RecursoLogo))
        {
            if (logo is not null)
            {
                hoja.AddPicture(logo)
                    .MoveTo(hoja.Cell(1, Math.Max(1, columnas)))
                    .WithSize(30, 30);
            }
        }

        // Nombre del reporte
        hoja.Cell(2, 1).Value = tituloReporte;
        hoja.Range(2, 1, 2, columnas).Merge();

        var subtitulo = hoja.Range(2, 1, 2, columnas);
        subtitulo.Style.Font.Bold = true;
        subtitulo.Style.Font.FontSize = 13;
        subtitulo.Style.Font.FontColor = XLColor.FromHtml("#6D28D9");

        hoja.Row(2).Height = 28;

        // Encabezados
        for (int i = 0; i < columnas; i++)
        {
            hoja.Cell(3, i + 1).Value = cabeceras[i];
        }

        var encabezado = hoja.Range(3, 1, 3, columnas);
        encabezado.Style.Fill.BackgroundColor = XLColor.FromHtml("#7C3AED");
        encabezado.Style.Font.FontColor = XLColor.White;
        encabezado.Style.Font.Bold = true;
        encabezado.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;

        hoja.Row(3).Height = 27;

        // Datos
        int filaActual = 4;

        foreach (var fila in filas)
        {
            for (int i = 0; i < fila.Count; i++)
            {
                var celda = hoja.Cell(filaActual, i + 1);
                object? valor = fila[i];

                switch (valor)
                {
                    case null:
                    case DBNull:
                        break;

                    case DateTime fecha:
                        celda.Value = fecha;
                        celda.Style.DateFormat.Format = "dd/mm/yyyy";
                        break;

                    case decimal numero:
                        celda.Value = Convert.ToDouble(numero);
                        celda.Style.NumberFormat.Format =
                            monedas.Contains(i) ? "#,##0.00" : "0.00";
                        break;

                    case int numero:
                        celda.Value = numero;
                        break;

                    case long numero:
                        celda.Value = numero;
                        break;

                    case double numero:
                        celda.Value = numero;
                        break;

                    case float numero:
                        celda.Value = Convert.ToDouble(numero);
                        break;

                    case bool booleano:
                        celda.Value = booleano;
                        break;

                    default:
                        celda.Value = valor.ToString() ?? string.Empty;
                        break;
                }

                if (fechas.Contains(i) && valor is DateTime)
                {
                    celda.Style.DateFormat.Format = "dd/mm/yyyy";
                }
            }

            if (filaActual % 2 == 0)
            {
                hoja.Range(filaActual, 1, filaActual, columnas)
                    .Style.Fill.BackgroundColor =
                        XLColor.FromHtml("#F5F3FF");
            }

            filaActual++;
        }

        // Resumen
        if (resumen is not null && resumen.Count > 0)
        {
            filaActual++;

            foreach (var dato in resumen)
            {
                hoja.Cell(filaActual, 1).Value = dato.Etiqueta;
                hoja.Cell(filaActual, 2).Value = dato.Valor;

                hoja.Cell(filaActual, 1).Style.Font.Bold = true;
                hoja.Cell(filaActual, 1).Style.Font.FontColor =
                    XLColor.FromHtml("#6D28D9");

                filaActual++;
            }
        }

        // Ajustes de presentación
        hoja.Columns(1, columnas).AdjustToContents();

        foreach (var columna in hoja.Columns(1, columnas))
        {
            if (columna.Width < 14)
                columna.Width = 14;

            if (columna.Width > 55)
                columna.Width = 55;
        }

        hoja.SheetView.FreezeRows(3);

        if (grafico is not null)
        {
            using var imagenGrafico = new MemoryStream(grafico, writable: false);
            hoja.AddPicture(imagenGrafico, tituloGrafico)
                .MoveTo(hoja.Cell(filaActual + 2, 1))
                .WithSize(650, altoGrafico);
        }

        // Generar XLSX genuino
        using var memoria = new MemoryStream();
        libro.SaveAs(memoria);

        return memoria.ToArray();
    }
}

public sealed class XlsxFilaResumen
{
    public string Etiqueta { get; set; } = string.Empty;
    public string Valor { get; set; } = string.Empty;
}
