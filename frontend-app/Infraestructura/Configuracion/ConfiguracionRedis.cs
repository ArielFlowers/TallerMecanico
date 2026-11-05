using System.Globalization;

namespace TallerMecanico.Infraestructura.Configuracion;

public sealed record ConfiguracionRedis(string? Host, int Puerto, int ExpiracionDashboardSegundos, string OrigenHost)
{
    public bool Habilitado => Host is not null;

    public static ConfiguracionRedis Leer(IConfiguration configuracion)
    {
        string? hostEntorno = NoVacio(configuracion["REDIS_HOST"]);
        string? host = hostEntorno ?? NoVacio(configuracion["Redis:Host"]);
        if (host is not null && Uri.CheckHostName(host) == UriHostNameType.Unknown)
            throw new InvalidOperationException("REDIS_HOST/Redis:Host debe ser un nombre DNS o una IP, sin credenciales, esquema ni puerto.");

        string puertoConfigurado = NoVacio(configuracion["REDIS_PORT"])
            ?? NoVacio(configuracion["Redis:Port"]) ?? "6379";
        if (!int.TryParse(puertoConfigurado, NumberStyles.Integer, CultureInfo.InvariantCulture, out int puerto)
            || puerto is < 1 or > 65535)
            throw new InvalidOperationException("REDIS_PORT/Redis:Port debe ser un entero entre 1 y 65535.");

        string ttlConfigurado = configuracion["Redis:ExpiracionDashboardSegundos"] ?? "60";
        if (!int.TryParse(ttlConfigurado, NumberStyles.Integer, CultureInfo.InvariantCulture, out int ttl) || ttl <= 0)
            throw new InvalidOperationException("Redis:ExpiracionDashboardSegundos debe ser un entero positivo.");

        return new(host, puerto, ttl, hostEntorno is not null ? "REDIS_HOST" : host is null ? "Sin configurar" : "Redis:Host");
    }

    public void RegistrarDiagnostico(ILogger logger, bool conectado)
    {
        string estado = !Habilitado ? "Deshabilitado" : conectado ? "Disponible" : "NoDisponible";
        logger.Log(Habilitado && !conectado ? LogLevel.Warning : LogLevel.Information,
            "CONFIGURACION REDIS | habilitado {Habilitado} | estado {Estado} | host {Host} | puerto {Puerto} | origen {OrigenHost} | TTLResumenSegundos {Ttl} | proveedorSesiones {Proveedor} | réplica {Replica}",
            Habilitado, estado, Host ?? "(sin host)", Puerto, OrigenHost, ExpiracionDashboardSegundos,
            Habilitado ? "RedisCache compartido" : "MemoryDistributedCache local, no compartido entre réplicas",
            Environment.MachineName);
    }

    private static string? NoVacio(string? texto) => string.IsNullOrWhiteSpace(texto) ? null : texto.Trim();
}
