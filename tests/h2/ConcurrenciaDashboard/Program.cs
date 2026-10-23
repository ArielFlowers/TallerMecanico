using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MySqlConnector;
using StackExchange.Redis;
using TallerMecanico.Aplicacion.Dto;
using TallerMecanico.Aplicacion.Puertos;
using TallerMecanico.Aplicacion.Servicios;
using TallerMecanico.Infraestructura.Cache;
using TallerMecanico.Infraestructura.Persistencia;
using TallerMecanico.Infraestructura.Persistencia.MySql;

var configuracion = new ConfigurationBuilder().AddJsonFile("/app/appsettings.json")
    .AddEnvironmentVariables().Build();
var conexion = new MySqlConnectionStringBuilder(configuracion.GetConnectionString("MySqlConnection")
    ?? throw new InvalidOperationException("Falta la conexión H2 explícita."));
if (conexion.Server != "mysql" || conexion.Database != "autotaller_h2_integracion"
    || conexion.UserID != "h2_app" || configuracion["REDIS_HOST"] != "redis"
    || configuracion["ASPNETCORE_ENVIRONMENT"] != "Development")
    throw new InvalidOperationException("Prueba permitida exclusivamente dentro del entorno H2 aislado.");

using var redis = ConnectionMultiplexer.Connect(new ConfigurationOptions
{
    EndPoints = { "redis:6379" }, AbortOnConnectFail = false,
    ConnectTimeout = 2000, SyncTimeout = 1000, ClientName = $"h2-barrera-{Environment.MachineName}"
});
var adapter = new RedisCacheAdapter(redis, NullLogger<RedisCacheAdapter>.Instance);
var barrera = new CacheConBarrera(adapter);
var fabrica = new MySqlConnectionFactory(configuracion);
var logger = new RegistroPrueba<DashboardCacheService>();
var dashboard = new DashboardCacheService(
    new DashboardService(new ServicioRepository(fabrica), new VehiculoRepository(fabrica), new MecanicoRepository(fabrica)),
    barrera, new OpcionesCacheDashboard(), TimeProvider.System, logger);
ResultadoDashboard resultado = dashboard.Obtener();
Console.WriteLine(JsonSerializer.Serialize(new
{
    evento = "final", replica = Environment.MachineName, resultado,
    publicaciones = barrera.Publicaciones, logs = logger.Mensajes
}));

// El proceso anfitrión libera explícitamente la primera publicación; no hay sleeps.
sealed class CacheConBarrera(ICacheResumenDashboardPort cache) : ICacheResumenDashboardPort
{
    public List<string> Publicaciones { get; } = [];
    public LecturaResumenDashboard ObtenerResumen() => cache.ObtenerResumen();
    public TimeSpan? TiempoRestanteResumen() => cache.TiempoRestanteResumen();
    public ResultadoEliminacionCache InvalidarResumen() => cache.InvalidarResumen();
    public EstadoPublicacionResumen PublicarResumen(LecturaResumenDashboard lectura, string valor, TimeSpan expiracion)
    {
        if (Publicaciones.Count == 0)
        {
            Console.WriteLine(JsonSerializer.Serialize(new
            {
                evento = "listo", replica = Environment.MachineName, generacion = lectura.Generacion,
                entrada = JsonSerializer.Deserialize<JsonElement>(valor)
            }));
            if (Console.ReadLine() != "continuar") throw new InvalidOperationException("Falta liberar la barrera de prueba.");
        }
        EstadoPublicacionResumen resultado = cache.PublicarResumen(lectura, valor, expiracion);
        Publicaciones.Add(resultado.ToString());
        return resultado;
    }
}

sealed class RegistroPrueba<T> : ILogger<T>
{
    public List<string> Mensajes { get; } = [];
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public bool IsEnabled(LogLevel logLevel) => true;
    public void Log<TState>(LogLevel nivel, EventId evento, TState estado, Exception? error,
        Func<TState, Exception?, string> formato) => Mensajes.Add(formato(estado, error));
}
