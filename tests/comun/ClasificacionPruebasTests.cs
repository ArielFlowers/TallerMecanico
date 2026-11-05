using System.Reflection;

namespace TallerMecanico.IntegrationTests;

[Trait("Area", "Backend")]
public sealed class ClasificacionPruebasTests
{
    [Fact]
    public void TodasLasPruebas_PertenecenAUnaSolaAreaDelCi()
    {
        string[] areasValidas = ["Backend", "API", "Datos", "Frontend"];
        MethodInfo[] pruebas = typeof(ClasificacionPruebasTests).Assembly.GetTypes()
            .SelectMany(tipo => tipo.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            .Where(metodo => metodo.CustomAttributes.Any(atributo => typeof(FactAttribute).IsAssignableFrom(atributo.AttributeType)))
            .ToArray();

        Assert.NotEmpty(pruebas);
        foreach (MethodInfo prueba in pruebas)
        {
            string[] areas = prueba.DeclaringType!.CustomAttributes.Concat(prueba.CustomAttributes)
                .Where(atributo => atributo.AttributeType == typeof(TraitAttribute)
                    && atributo.ConstructorArguments[0].Value as string == "Area")
                .Select(atributo => (string)atributo.ConstructorArguments[1].Value!)
                .ToArray();

            Assert.True(areas.Length == 1,
                $"{prueba.DeclaringType.Name}.{prueba.Name} debe declarar exactamente un Trait Area; tiene {areas.Length}.");
            Assert.Contains(areas[0], areasValidas);
        }
    }
}
