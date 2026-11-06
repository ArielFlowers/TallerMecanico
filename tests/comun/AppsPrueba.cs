using System.Diagnostics;

namespace TallerMecanico.IntegrationTests;

// Ubica los ensamblados compilados de cada app del monorepo.
internal static class AppsPrueba
{
    private static readonly string Configuracion =
        new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;

    public static string Dll(string carpeta, string ensamblado) => Path.Combine(
        MySqlAisladoFixture.RaizProyecto, carpeta, "bin", Configuracion, "net10.0", ensamblado + ".dll");

    public static string DllFrontend => Dll("frontend-app", "TallerMecanico.Frontend");
    public static string DllOrdenes => Dll("backend-services/servicio-ordenes", "TallerMecanico.ServicioOrdenes");

    public static void CrearEsquema(string cadenaConexion)
    {
        var inicio = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = Path.Combine(MySqlAisladoFixture.RaizProyecto, "backend-services", "servicio-ordenes"),
            UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true
        };
        inicio.ArgumentList.Add(DllOrdenes);
        inicio.ArgumentList.Add("--inicializar-esquema");
        inicio.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";
        inicio.Environment["ConnectionStrings__MySqlConnection"] = cadenaConexion;
        using var proceso = Process.Start(inicio)!;
        Task<string> salida = proceso.StandardOutput.ReadToEndAsync();
        Task<string> errores = proceso.StandardError.ReadToEndAsync();
        proceso.WaitForExit();
        if (proceso.ExitCode != 0)
            throw new InvalidOperationException($"No se pudo crear el esquema: {salida.Result}{errores.Result}");
    }
}
