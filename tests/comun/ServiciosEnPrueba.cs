using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

namespace TallerMecanico.IntegrationTests;

// Levanta las APIs del monorepo como procesos reales, contra una base aislada.
internal sealed class ServiciosEnPrueba : IDisposable
{
    public const string ClaveInterna = "clave-interna-de-pruebas";

    private static readonly (string Carpeta, string Ensamblado, string VariableUrl)[] Servicios =
    [
        ("backend-services/servicio-ordenes", "TallerMecanico.ServicioOrdenes", "Servicios__OrdenesUrl"),
        ("backend-services/servicio-usuarios", "TallerMecanico.ServicioUsuarios", "Servicios__UsuariosUrl"),
        ("backend-services/servicio-reportes", "TallerMecanico.ServicioReportes", "Servicios__ReportesUrl"),
    ];

    private readonly List<Process> _procesos = [];
    private readonly Dictionary<string, string> _urls = [];

    public static ServiciosEnPrueba Iniciar(string cadenaConexion)
    {
        var servicios = new ServiciosEnPrueba();
        try
        {
            foreach (var (carpeta, ensamblado, variableUrl) in Servicios)
            {
                string url = $"http://127.0.0.1:{PuertoLibre()}";
                servicios._procesos.Add(Lanzar(carpeta, ensamblado, url, cadenaConexion));
                servicios._urls[variableUrl] = url;
            }
            foreach (string url in servicios._urls.Values) EsperarSalud(url);
            return servicios;
        }
        catch
        {
            servicios.Dispose();
            throw;
        }
    }

    public string Url(string variable) => _urls[variable];

    public void Configurar(ProcessStartInfo inicio)
    {
        inicio.Environment["ClaveInterna"] = ClaveInterna;
        foreach (var (variable, url) in _urls) inicio.Environment[variable] = url;
    }

    public void Dispose()
    {
        foreach (Process proceso in _procesos)
        {
            if (!proceso.HasExited) proceso.Kill(entireProcessTree: true);
            proceso.WaitForExit();
            proceso.Dispose();
        }
        _procesos.Clear();
    }

    private static Process Lanzar(string carpeta, string ensamblado, string url, string cadenaConexion)
    {
        var inicio = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = Path.Combine(MySqlAisladoFixture.RaizProyecto, carpeta),
            UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = false, RedirectStandardError = false
        };
        inicio.ArgumentList.Add(AppsPrueba.Dll(carpeta, ensamblado));
        inicio.ArgumentList.Add("--urls");
        inicio.ArgumentList.Add(url);
        inicio.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";
        inicio.Environment["ConnectionStrings__MySqlConnection"] = cadenaConexion;
        inicio.Environment["ClaveInterna"] = ClaveInterna;
        return Process.Start(inicio)!;
    }

    private static int PuertoLibre()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int puerto = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return puerto;
    }

    private static void EsperarSalud(string url)
    {
        using var cliente = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
        for (int intento = 0; intento < 120; intento++)
        {
            try
            {
                if (cliente.GetAsync($"{url}/salud").GetAwaiter().GetResult().IsSuccessStatusCode) return;
            }
            catch (HttpRequestException) { }
            catch (TaskCanceledException) { }
            Thread.Sleep(250);
        }
        throw new TimeoutException($"La API {url} no inició.");
    }
}
