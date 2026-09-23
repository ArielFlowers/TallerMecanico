using System.Collections.ObjectModel;

namespace TallerMecanico.Models;

public static class CatalogoVehiculos
{
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
