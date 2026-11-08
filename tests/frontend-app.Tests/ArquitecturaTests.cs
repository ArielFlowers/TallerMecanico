namespace TallerMecanico.IntegrationTests;

[Trait("Area", "Backend")]
public sealed class ArquitecturaTests
{
    [Fact]
    public void Frontend_NoReferenciaPersistenciaNiImplementacionesDeLosServicios()
    {
        string[] referencias = typeof(TallerMecanico.Api.UsuariosApi).Assembly.GetReferencedAssemblies()
            .Select(referencia => referencia.Name!).ToArray();
        string[] dependenciasBackend = ["MySqlConnector", "MailKit", "QuestPDF", "ClosedXML",
            "TallerMecanico.ServicioUsuarios", "TallerMecanico.ServicioOrdenes", "TallerMecanico.ServicioReportes"];
        Assert.Empty(referencias.Intersect(dependenciasBackend));
        Assert.Contains("TallerMecanico.Contratos", referencias);
    }

    private static readonly string[] Prohibidos = ["TallerMecanico.Infraestructura", "TallerMecanico.Aplicacion", "TallerMecanico.Dominio", "MySqlConnector", "QuestPDF", "ClosedXML"];

    // Deuda ya asignada en el plan del entregable. Quitar cada archivo al corregirlo.
    private static readonly string[] Pendientes = [];

    [Fact]
    public void Frontend_NoMencionaLasCapasDeLosServicios()
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
        foreach (string capa in new[] { "Api", "Configuracion", "Pages", "Seguridad", "Sesion" })
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
        return MySqlAisladoFixture.RaizFrontend;
    }
}
