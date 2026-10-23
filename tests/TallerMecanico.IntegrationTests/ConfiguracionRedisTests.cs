using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using TallerMecanico.Infraestructura.Configuracion;

namespace TallerMecanico.IntegrationTests;

public sealed class ConfiguracionRedisTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void HostEntornoVacio_NoOcultaAlternativaValida(string? entorno)
    {
        var opciones = Leer(("REDIS_HOST", entorno), ("Redis:Host", "redis-alternativo"));
        Assert.True(opciones.Habilitado);
        Assert.Equal("redis-alternativo", opciones.Host);
        Assert.Equal("Redis:Host", opciones.OrigenHost);
    }

    [Fact]
    public void HostEntornoValido_TienePrecedenciaYSeLimpianEspacios()
    {
        var opciones = Leer(("REDIS_HOST", " redis-entorno "), ("Redis:Host", "redis-archivo"));
        Assert.Equal("redis-entorno", opciones.Host);
        Assert.Equal("REDIS_HOST", opciones.OrigenHost);
    }

    [Fact]
    public void ProveedorMasReciente_ConservaPrecedenciaDeIConfiguration()
    {
        var configuracion = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Redis:Host"] = "archivo" })
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Redis:Host"] = "entorno" })
            .Build();
        Assert.Equal("entorno", ConfiguracionRedis.Leer(configuracion).Host);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void SinHostValido_RedisPermaneceDeshabilitado(string? host)
    {
        var opciones = Leer(("REDIS_HOST", host), ("Redis:Host", host));
        Assert.False(opciones.Habilitado);
        Assert.Null(opciones.Host);
        Assert.Equal(60, opciones.ExpiracionDashboardSegundos);
        Assert.Equal(6379, opciones.Puerto);
    }

    [Theory]
    [InlineData("redis")]
    [InlineData("redis.interno")]
    [InlineData("127.0.0.1")]
    [InlineData("::1")]
    public void HostDnsOIp_SeAcepta(string host) => Assert.Equal(host, Leer(("REDIS_HOST", host)).Host);

    [Theory]
    [InlineData("redis://usuario:secreto@redis")]
    [InlineData("redis,password=secreto")]
    [InlineData("redis:6379")]
    [InlineData("redis interno")]
    public void HostNoVacioInvalido_SeRechazaSinUsarAlternativaNiExponerContenido(string host)
    {
        var error = Assert.Throws<InvalidOperationException>(() => Leer(("REDIS_HOST", host), ("Redis:Host", "valido")));
        Assert.DoesNotContain(host, error.Message);
        Assert.DoesNotContain("secreto", error.Message);
    }

    [Theory]
    [InlineData("1", 1)]
    [InlineData("60", 60)]
    [InlineData("120", 120)]
    public void TtlPositivo_SeConserva(string configurado, int esperado) =>
        Assert.Equal(esperado, Leer(("Redis:ExpiracionDashboardSegundos", configurado)).ExpiracionDashboardSegundos);

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("texto")]
    [InlineData("1.5")]
    [InlineData("2147483648")]
    public void TtlInvalido_FallaAntesDeCrearConexiones(string ttl)
    {
        var error = Assert.Throws<InvalidOperationException>(() => Leer(("Redis:ExpiracionDashboardSegundos", ttl)));
        Assert.Equal("Redis:ExpiracionDashboardSegundos debe ser un entero positivo.", error.Message);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("65536")]
    [InlineData("abc")]
    public void PuertoInvalido_SeRechaza(string puerto) =>
        Assert.Throws<InvalidOperationException>(() => Leer(("REDIS_PORT", puerto)));

    [Fact]
    public void PuertoEntornoVacio_UsaPuertoAlternativo() =>
        Assert.Equal(6380, Leer(("REDIS_PORT", " "), ("Redis:Port", "6380")).Puerto);

    [Theory]
    [InlineData("redis", true, "Disponible", "RedisCache compartido")]
    [InlineData("redis", false, "NoDisponible", "RedisCache compartido")]
    [InlineData(null, false, "Deshabilitado", "no compartido entre réplicas")]
    public void Diagnostico_DistingueEstadoYProveedorSinInventarLecturas(
        string? host, bool conectado, string estado, string proveedor)
    {
        var logger = new RegistradorCachePrueba<ConfiguracionRedis>();
        Leer(("REDIS_HOST", host)).RegistrarDiagnostico(logger, conectado);
        string mensaje = Assert.Single(logger.Mensajes);
        Assert.Contains("CONFIGURACION REDIS", mensaje);
        Assert.Contains(estado, mensaje);
        Assert.Contains(proveedor, mensaje);
        Assert.Contains("puerto 6379", mensaje);
        Assert.Contains("TTLResumenSegundos 60", mensaje);
        Assert.DoesNotContain("HIT", mensaje);
        Assert.DoesNotContain("MISS", mensaje);
        Assert.Equal(host is not null && !conectado ? LogLevel.Warning : LogLevel.Information, Assert.Single(logger.Niveles));
    }

    private static ConfiguracionRedis Leer(params (string Clave, string? Valor)[] valores) =>
        ConfiguracionRedis.Leer(new ConfigurationBuilder().AddInMemoryCollection(
            valores.ToDictionary(par => par.Clave, par => par.Valor)).Build());
}
