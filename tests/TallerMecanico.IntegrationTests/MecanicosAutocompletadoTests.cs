using System.Text.Json;
using Microsoft.Extensions.Configuration;
using TallerMecanico.Aplicacion.Servicios;
using TallerMecanico.Aplicacion.Validaciones;
using TallerMecanico.Infraestructura.Persistencia;
using TallerMecanico.Infraestructura.Persistencia.Fabricas;

namespace TallerMecanico.IntegrationTests;

public sealed class MecanicosAutocompletadoTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("a")]
    public async Task TerminoCorto_NoAbreConexion(string? termino)
    {
        var configuracion = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:MySqlConnection"] = "Server=servidor-inexistente;Database=no_usar;User ID=prueba"
        }).Build();
        var servicio = new MecanicoService(
            new CreadorMecanico(new MySqlConnectionFactory(configuracion)), new ValidacionMecanicos());
        Assert.Empty(await servicio.SugerirAsync(termino));
        Assert.Empty(await servicio.SugerirAsync(new string('a', 101)));
    }

    [MySqlAisladoFact]
    public async Task MySql_PrefijoPalabrasCompuestasTildesYApellidoOpcional()
    {
        using var aislada = new MySqlAisladoFixture();
        var servicio = CrearServicio(aislada);
        Crear(servicio, "7812345", "María Fernanda", "Pérez", "De La Cruz");
        Crear(servicio, "1278345", "Juan Carlos", "Muñoz", null);

        Assert.Equal("María Fernanda Pérez De La Cruz", Assert.Single(await servicio.SugerirAsync("78")).TextoPrincipal);
        Assert.Empty(await servicio.SugerirAsync("2345"));
        foreach (string termino in new[] { "MARIA", "fer", "perez", "maria   per", "la cru", "de la cruz" })
            Assert.Equal("María Fernanda Pérez De La Cruz", Assert.Single(await servicio.SugerirAsync(termino)).TextoPrincipal);
        Assert.Empty(await servicio.SugerirAsync("aria"));
        var sinSegundo = Assert.Single(await servicio.SugerirAsync("juan munoz"));
        Assert.Equal("Juan Carlos Muñoz", sinSegundo.TextoPrincipal);
        Assert.DoesNotContain("  ", sinSegundo.TextoPrincipal);

        aislada.Ejecutar("ALTER TABLE Mecanicos MODIFY SegundoApellido VARCHAR(100) NULL;");
        aislada.Ejecutar("UPDATE Mecanicos SET SegundoApellido = NULL WHERE Ci = '1278345';");
        Assert.Equal(sinSegundo, Assert.Single(await servicio.SugerirAsync("munoz")));
    }

    [MySqlAisladoFact]
    public async Task MySql_LimitaDiezEscapaComodinesYNoModificaDatos()
    {
        using var aislada = new MySqlAisladoFixture();
        var servicio = CrearServicio(aislada);
        for (int indice = 0; indice < 13; indice++)
            Crear(servicio, $"78{indice:00000}", "José Carlos", "Pérez", "");
        var antes = JsonSerializer.Serialize(servicio.Obtener());

        // Se prueba directamente el puerto/repositorio, no un recorte posterior del servicio.
        var repositorio = new MecanicoRepository(aislada.Factory);
        var sugerencias = await repositorio.SugerirAsync("78");
        Assert.Equal(10, sugerencias.Count);
        Assert.Equal(10, sugerencias.Select(sugerencia => sugerencia.Id).Distinct().Count());
        Assert.Equal(sugerencias.ToArray(), (await repositorio.SugerirAsync("78")).ToArray());
        foreach (string termino in new[] { "%%", "__", "!!", "' OR 1=1 --", "<script>", "78%", "78_" })
            Assert.Empty(await servicio.SugerirAsync(termino));
        Assert.Equal(antes, JsonSerializer.Serialize(servicio.Obtener()));
    }

    [MySqlAisladoFact]
    public async Task MySql_IdExactoYBusquedaManualConservanSemanticasSeparadas()
    {
        using var aislada = new MySqlAisladoFixture();
        var servicio = CrearServicio(aislada);
        Crear(servicio, "7812345", "Juan Carlos", "Pérez", "");
        Crear(servicio, "1278345", "Juan Carlos", "Pérez", "");
        var formulario = MecanicosTests.FormularioValido("4567890");
        formulario.ComplementoCi = "1A";
        Assert.Empty(servicio.Crear(formulario));

        int id = Assert.Single(await servicio.SugerirAsync("78")).Id;
        Assert.Equal("7812345", Assert.Single(servicio.ObtenerPorId(id)).Ci);
        Assert.Empty(servicio.ObtenerPorId(-1));
        Assert.Equal(3, servicio.Obtener("78").Count);
        foreach (string termino in new[] { "Motores", "71234567", "Masculino", "Carlos", "érez" })
            Assert.Equal(3, servicio.Obtener(termino).Count);
        Assert.Single(servicio.Obtener("1A"));
        Assert.Equal(3, servicio.Obtener().Count);
    }

    private static MecanicoService CrearServicio(MySqlAisladoFixture aislada) =>
        new(new CreadorMecanico(aislada.Factory), new ValidacionMecanicos());

    internal static void Crear(MecanicoService servicio, string ci, string nombres, string primerApellido, string? segundoApellido)
    {
        var formulario = MecanicosTests.FormularioValido(ci);
        formulario.Nombres = nombres;
        formulario.PrimerApellido = primerApellido;
        formulario.SegundoApellido = segundoApellido;
        Assert.Empty(servicio.Crear(formulario));
    }
}
