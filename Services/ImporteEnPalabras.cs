using System.Globalization;
using Humanizer;

namespace TallerMecanico.Services;

public sealed class ImporteEnPalabras
{
    private const decimal ImporteMaximo = 9999999999999999.99m;
    private const int DecimalesMonetarios = 2;
    private const int CentavosPorBoliviano = 100;
    private const long UnMillon = 1000000;
    private static readonly CultureInfo CulturaEspanola = CultureInfo.GetCultureInfo("es");

    public string Convertir(decimal total)
    {
        if (total < 0 || total > ImporteMaximo || total != decimal.Round(total, DecimalesMonetarios))
        {
            throw new ArgumentOutOfRangeException(nameof(total), "El importe debe respetar el rango monetario y tener hasta dos decimales.");
        }

        long bolivianos = decimal.ToInt64(decimal.Truncate(total));
        int centavos = decimal.ToInt32((total - bolivianos) * CentavosPorBoliviano);
        string numero = bolivianos.ToWords(WordForm.Abbreviation, GrammaticalGender.Masculine, CulturaEspanola);
        string palabras = CulturaEspanola.TextInfo.ToTitleCase(numero).Replace(" Y ", " y ", StringComparison.Ordinal);
        string moneda = bolivianos == 1 ? "Boliviano" : "Bolivianos";
        string enlace = bolivianos >= UnMillon && bolivianos % UnMillon == 0 ? " de " : " ";
        string fraccion = centavos == 0 ? string.Empty : $" con {centavos:D2}/{CentavosPorBoliviano}";

        return $"Son {palabras}{enlace}{moneda}{fraccion}.";
    }
}
