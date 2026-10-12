using System.Collections.ObjectModel;
using System.Globalization;

namespace TallerMecanico.Models;

public static class CatalogoVehiculos
{
    public static StringComparer ComparadorAlfabetico { get; } =
        StringComparer.Create(CultureInfo.GetCultureInfo("es-BO"), ignoreCase: true);

    public static IEnumerable<string> MarcasOrdenadas =>
        Marcas.Keys.OrderBy(marca => marca, ComparadorAlfabetico);

    public static IReadOnlyDictionary<string, IReadOnlyList<string>> CatalogoOrdenado =>
        MarcasOrdenadas.ToDictionary(marca => marca, ObtenerModelosOrdenados);

    public static IReadOnlyList<string> ObtenerModelosOrdenados(string marca) =>
        Marcas.TryGetValue(marca, out var modelos)
            ? modelos.OrderBy(modelo => modelo, ComparadorAlfabetico).ToArray()
            : [];

    public static IReadOnlyDictionary<string, IReadOnlyList<string>> Marcas { get; } =
        new ReadOnlyDictionary<string, IReadOnlyList<string>>(
            new Dictionary<string, IReadOnlyList<string>>
            {
                ["Toyota"] = Array.AsReadOnly(new[] { "Corolla", "Yaris", "Hilux", "RAV4", "Land Cruiser" }),
                ["Nissan"] = Array.AsReadOnly(new[] { "Sentra", "Versa", "March", "Frontier", "X-Trail" }),
                ["Suzuki"] = Array.AsReadOnly(new[] { "Swift", "Alto", "Baleno", "Vitara", "Jimny" }),
                ["Hyundai"] = Array.AsReadOnly(new[] { "Accent", "Elantra", "Tucson", "Santa Fe" }),
                ["Kia"] = Array.AsReadOnly(new[] { "Picanto", "Rio", "Cerato", "Sportage", "Sorento" }),
                ["Chevrolet"] = Array.AsReadOnly(new[] { "Onix", "Aveo", "Tracker", "Captiva", "S10" }),
                ["Mitsubishi"] = Array.AsReadOnly(new[] { "Lancer", "Mirage", "ASX", "Outlander", "L200" })
            });
}
