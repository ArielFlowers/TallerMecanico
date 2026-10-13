using System.Globalization;
using TallerMecanico.Aplicacion.Servicios;

namespace TallerMecanico.IntegrationTests;

public sealed class ImporteEnPalabrasTests
{
    [Theory]
    [InlineData("0.00", "Son Cero Bolivianos.")]
    [InlineData("1.00", "Son Un Boliviano.")]
    [InlineData("21.00", "Son Veintiún Bolivianos.")]
    [InlineData("31.00", "Son Treinta y Un Bolivianos.")]
    [InlineData("101.00", "Son Ciento Un Bolivianos.")]
    [InlineData("150.00", "Son Ciento Cincuenta Bolivianos.")]
    [InlineData("1250.00", "Son Mil Doscientos Cincuenta Bolivianos.")]
    [InlineData("31.50", "Son Treinta y Un Bolivianos con 50/100.")]
    [InlineData("0.01", "Son Cero Bolivianos con 01/100.")]
    [InlineData("1.05", "Son Un Boliviano con 05/100.")]
    [InlineData("21000.01", "Son Veintiún Mil Bolivianos con 01/100.")]
    [InlineData("1000000.00", "Son Un Millón de Bolivianos.")]
    [InlineData("21000000.50", "Son Veintiún Millones de Bolivianos con 50/100.")]
    public void Conversion_UsaGramaticaMonedaYCentavosExactos(string total, string esperado)
    {
        Assert.Equal(esperado, new ImporteEnPalabras().Convertir(decimal.Parse(total, CultureInfo.InvariantCulture)));
    }

    [Fact]
    public void Conversion_SoportaElMaximoDeDecimal18ConDosDecimales()
    {
        string literal = new ImporteEnPalabras().Convertir(9999999999999999.99m);
        Assert.StartsWith("Son Nueve Mil", literal);
        Assert.EndsWith("Noventa y Nueve Bolivianos con 99/100.", literal);
    }

    [Theory]
    [InlineData("-0.01")]
    [InlineData("1.001")]
    [InlineData("10000000000000000")]
    public void Conversion_NoRedondeaNiAceptaImportesFueraDelEsquema(string total)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ImporteEnPalabras().Convertir(decimal.Parse(total, CultureInfo.InvariantCulture)));
    }
}
