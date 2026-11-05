namespace TallerMecanico.IntegrationTests;

[Trait("Area", "Backend")]
public sealed class ArquitecturaTests
{
    private static readonly string[] Prohibidos = ["TallerMecanico.Infraestructura", "MySqlConnector"];

    // Deuda ya asignada en el plan del entregable. Quitar cada archivo al corregirlo.
    private static readonly string[] Pendientes =
    [
        "Aplicacion/Servicios/ClienteService.cs",
        "Aplicacion/Servicios/MecanicoService.cs",
        "Aplicacion/Servicios/ProductoService.cs",
        "Aplicacion/Servicios/ServicioService.cs",
        "Aplicacion/Servicios/UsuarioAdministracionService.cs",
        "Aplicacion/Servicios/VehiculoService.cs"
    ];

    [Fact]
    public void Dominio_Aplicacion_Y_Pages_NoDependenDeInfraestructura()
    {
        string raiz = BuscarRaiz();

        string[] violaciones = BuscarArchivos(raiz)
            .Where(relativo => !Pendientes.Contains(relativo))
            .Where(relativo => TieneDependenciaProhibida(raiz, relativo))
            .ToArray();

        Assert.True(violaciones.Length == 0, "Dependencias prohibidas en: " + string.Join(", ", violaciones));
    }

    [Fact]
    public void Pendientes_SeQuitanDeLaListaAlCorregirse()
    {
        string raiz = BuscarRaiz();

        string[] corregidos = Pendientes
            .Where(relativo => !TieneDependenciaProhibida(raiz, relativo))
            .ToArray();

        Assert.True(corregidos.Length == 0, "Quitar de Pendientes: " + string.Join(", ", corregidos));
    }

    private static bool TieneDependenciaProhibida(string raiz, string relativo)
    {
        string contenido = File.ReadAllText(Path.Combine(raiz, relativo));
        return Prohibidos.Any(contenido.Contains);
    }

    private static IEnumerable<string> BuscarArchivos(string raiz)
    {
        foreach (string capa in new[] { "Dominio", "Aplicacion", "Pages" })
        {
            foreach (string ruta in Directory.EnumerateFiles(Path.Combine(raiz, capa), "*", SearchOption.AllDirectories))
            {
                if (ruta.EndsWith(".cs") || ruta.EndsWith(".cshtml"))
                    yield return Path.GetRelativePath(raiz, ruta).Replace('\\', '/');
            }
        }
    }

    private static string BuscarRaiz()
    {
        DirectoryInfo? carpeta = new(AppContext.BaseDirectory);
        while (carpeta is not null && !File.Exists(Path.Combine(carpeta.FullName, "TallerMecanico.csproj")))
            carpeta = carpeta.Parent;

        return carpeta?.FullName ?? throw new InvalidOperationException("No se encontró la raíz del repositorio.");
    }
}
