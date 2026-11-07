namespace TallerMecanico.IntegrationTests;

[Trait("Area", "Backend")]
// Hexagonal: el núcleo (Dominio y Aplicacion) no conoce a sus adaptadores.
public sealed class ArquitecturaTests
{
    private static readonly string[] Prohibidos = ["TallerMecanico.Infraestructura", "MySqlConnector", "QuestPDF", "ClosedXML", "MailKit"];

    // Quitar cada archivo de la lista al corregirlo.
    private static readonly string[] Pendientes = [];

    [Fact]
    public void Dominio_Y_Aplicacion_NoDependenDeInfraestructura()
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
        foreach (string capa in new[] { "Dominio", "Aplicacion" })
        {
            if (!Directory.Exists(Path.Combine(raiz, capa))) continue;
            foreach (string ruta in Directory.EnumerateFiles(Path.Combine(raiz, capa), "*", SearchOption.AllDirectories))
            {
                if (ruta.EndsWith(".cs") || ruta.EndsWith(".cshtml"))
                    yield return Path.GetRelativePath(raiz, ruta).Replace('\\', '/');
            }
        }
    }

    private static string BuscarRaiz()
    {
        return Path.Combine(MySqlAisladoFixture.RaizProyecto, "backend-services/servicio-reportes");
    }
}
