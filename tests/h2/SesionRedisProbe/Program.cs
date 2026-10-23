using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Session;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MySqlConnector;
using StackExchange.Redis;
using TallerMecanico.Aplicacion.Puertos;
using TallerMecanico.Infraestructura.Sesion;

var configuracion = new ConfigurationBuilder().AddJsonFile("/app/appsettings.json")
    .AddEnvironmentVariables().Build();
var conexion = new MySqlConnectionStringBuilder(configuracion.GetConnectionString("MySqlConnection")
    ?? throw new InvalidOperationException("Falta conexión H2 explícita."));
if (conexion.Server != "mysql" || conexion.Database != "autotaller_h2_integracion"
    || conexion.UserID != "h2_app" || configuracion["REDIS_HOST"] != "redis"
    || configuracion["ASPNETCORE_ENVIRONMENT"] != "Development")
    throw new InvalidOperationException("Auxiliar permitido exclusivamente dentro de H2 aislado.");

if (args.FirstOrDefault() == "configuracion")
{
    var casos = new Dictionary<string, string[]>
    {
        ["hostVacioConAlternativa"] = ["--REDIS_HOST=", "--Redis:Host=redis"],
        ["ttlCero"] = ["--Redis:ExpiracionDashboardSegundos=0"],
        ["ttlNegativo"] = ["--Redis:ExpiracionDashboardSegundos=-1"],
        ["deshabilitado"] = ["--REDIS_HOST=", "--Redis:Host="],
        ["noDisponible"] = ["--REDIS_PORT=6380"]
    };
    var resultados = new List<object>();
    foreach (var (nombre, argumentos) in casos)
    {
        var inicio = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = "/app", RedirectStandardOutput = true, RedirectStandardError = true,
            UseShellExecute = false, CreateNoWindow = true
        };
        inicio.ArgumentList.Add("/app/TallerMecanico.dll");
        inicio.ArgumentList.Add("--urls=http://127.0.0.1:8095");
        foreach (string argumento in argumentos) inicio.ArgumentList.Add(argumento);
        using var proceso = Process.Start(inicio)!;
        Task<string> salida = proceso.StandardOutput.ReadToEndAsync(), error = proceso.StandardError.ReadToEndAsync();
        string? redis = null;
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
        try
        {
            using var limite = new CancellationTokenSource(TimeSpan.FromSeconds(25));
            while (!proceso.HasExited && !limite.IsCancellationRequested)
            {
                try
                {
                    using var respuesta = await http.GetAsync("http://127.0.0.1:8095/h2/redis", limite.Token);
                    if (respuesta.IsSuccessStatusCode)
                    {
                        using var json = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync());
                        redis = json.RootElement.GetProperty("redis").GetString();
                        break;
                    }
                }
                catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException) { }
                await Task.Delay(150);
            }
        }
        finally
        {
            if (!proceso.HasExited) proceso.Kill(entireProcessTree: true);
            await proceso.WaitForExitAsync();
        }
        string stdout = await salida, stderr = await error;
        resultados.Add(new
        {
            caso = nombre, inicioHttp = redis is not null, redis,
            diagnostico = stdout.Split('\n').Where(linea => linea.Contains("CONFIGURACION REDIS")).Select(linea => linea.Trim()).ToArray(),
            rechazoTtl = stderr.Contains("Redis:ExpiracionDashboardSegundos debe ser un entero positivo.")
        });
    }
    Console.WriteLine(JsonSerializer.Serialize(resultados));
    return;
}

using JsonDocument solicitud = JsonDocument.Parse(Console.ReadLine() ?? "{}");
JsonElement peticion = solicitud.RootElement;
string clave = peticion.GetProperty("claveSesion").GetString()!;
if (clave.Length < 10 || clave.Length > 200 || clave.Contains(':') || clave.Any(char.IsWhiteSpace))
    throw new InvalidOperationException("Identificador de sesión H2 inválido.");
using var multiplexer = ConnectionMultiplexer.Connect("redis:6379,abortConnect=false,connectTimeout=2000,syncTimeout=1000,asyncTimeout=1000");
var servicios = new ServiceCollection();
servicios.AddLogging(opciones => opciones.SetMinimumLevel(LogLevel.None));
servicios.AddStackExchangeRedisCache(opciones =>
{
    opciones.InstanceName = "autotaller:sesion:";
    opciones.ConnectionMultiplexerFactory = () => Task.FromResult<IConnectionMultiplexer>(multiplexer);
});
servicios.AddSession();
using ServiceProvider proveedor = servicios.BuildServiceProvider();
ISession sesion = proveedor.GetRequiredService<ISessionStore>().Create(
    clave, TimeSpan.FromMinutes(30), TimeSpan.FromSeconds(3), () => true, false);
await sesion.LoadAsync();
var contexto = new HttpContextAccessor { HttpContext = new DefaultHttpContext { Session = sesion } };
var usuario = new UsuarioH2();
var tokens = new TokenOrdenSesion(contexto, usuario, TimeProvider.System);
Guid token = peticion.GetProperty("token").GetGuid();
Guid ordenId = peticion.GetProperty("ordenId").GetGuid();
Guid vehiculoId = peticion.GetProperty("vehiculoId").GetGuid();
string modo = peticion.GetProperty("modo").GetString()!;
if (modo == "vencerSobres")
{
    foreach (string nombre in new[] { $"Ordenes:Token:{token:N}", $"Ordenes:Borrador:{ordenId:N}", $"Vehiculos:Borrador:{vehiculoId:N}" })
    {
        var contenido = JsonNode.Parse(sesion.GetString(nombre) ?? throw new InvalidOperationException("Falta dato de prueba."))!;
        contenido["VenceUtc"] = DateTime.UtcNow.AddMinutes(-1);
        sesion.SetString(nombre, contenido.ToJsonString());
    }
}
else if (modo != "consultar") throw new InvalidOperationException("Modo de prueba no permitido.");
bool tokenValido = tokens.EsValido(token);
var orden = new BorradorOrdenSesion(contexto, usuario).Obtener(ordenId);
var vehiculo = new BorradorVehiculoSesion(contexto, usuario).Obtener(vehiculoId);
await sesion.CommitAsync();
Console.WriteLine(JsonSerializer.Serialize(new
{
    replica = Environment.MachineName, tokenValido, ordenDisponible = orden is not null,
    vehiculoDisponible = vehiculo is not null, camposOrden = orden is null ? null : new { orden.VehiculoId, orden.MecanicoId },
    placaVehiculo = vehiculo?.Formulario.Placa
}));

sealed class UsuarioH2 : ICurrentUser
{
    public bool EstaAutenticado => true;
    public int? UsuarioId => 1;
    public string Username => "h2_administrador";
    public string Rol => "Administrador";
}
