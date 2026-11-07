using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TallerMecanico.Api;

namespace TallerMecanico.IntegrationTests;

// HTTPS entre el Frontend y las APIs: solo se confía en la CA interna y se valida el nombre del servicio.
[Trait("Area", "API")]
public sealed class HttpsInternoTests
{
    [Fact]
    public async Task CertificadoDeLaCaInterna_PermiteLlamarAlServicio()
    {
        using var ca = CrearCa("CA interna de prueba");
        await using var servicio = await IniciarServicioAsync(CrearCertificado(ca, "localhost"));
        using var cliente = new HttpClient(RegistroApis.CrearManejador(GuardarPem(ca)));

        using var respuesta = await cliente.GetAsync(servicio.Url("localhost"));

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
    }

    [Fact]
    public async Task CertificadoDeOtraCa_SeRechaza()
    {
        using var ca = CrearCa("CA interna de prueba");
        using var otraCa = CrearCa("CA desconocida");
        await using var servicio = await IniciarServicioAsync(CrearCertificado(otraCa, "localhost"));
        using var cliente = new HttpClient(RegistroApis.CrearManejador(GuardarPem(ca)));

        await Assert.ThrowsAsync<HttpRequestException>(() => cliente.GetAsync(servicio.Url("localhost")));
    }

    [Fact]
    public async Task NombreDelServicioDistinto_SeRechazaAunqueLaCaSeaCorrecta()
    {
        using var ca = CrearCa("CA interna de prueba");
        await using var servicio = await IniciarServicioAsync(CrearCertificado(ca, "servicio-ordenes"));
        using var cliente = new HttpClient(RegistroApis.CrearManejador(GuardarPem(ca)));

        await Assert.ThrowsAsync<HttpRequestException>(() => cliente.GetAsync(servicio.Url("localhost")));
    }

    private static X509Certificate2 CrearCa(string nombre)
    {
        using var clave = RSA.Create(2048);
        var solicitud = new CertificateRequest($"CN={nombre}", clave, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        solicitud.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
        solicitud.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign, true));
        return solicitud.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
    }

    private static X509Certificate2 CrearCertificado(X509Certificate2 ca, string nombreServidor)
    {
        using var clave = RSA.Create(2048);
        var solicitud = new CertificateRequest($"CN={nombreServidor}", clave, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var nombres = new SubjectAlternativeNameBuilder();
        nombres.AddDnsName(nombreServidor);
        solicitud.CertificateExtensions.Add(nombres.Build());
        solicitud.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension([new Oid("1.3.6.1.5.5.7.3.1")], false));
        using var firmado = solicitud.Create(ca, DateTimeOffset.UtcNow.AddHours(-1), DateTimeOffset.UtcNow.AddHours(1),
            RandomNumberGenerator.GetBytes(8));
        using var conClave = firmado.CopyWithPrivateKey(clave);
        // Kestrel en Windows necesita una clave que no sea efímera.
        return X509CertificateLoader.LoadPkcs12(conClave.Export(X509ContentType.Pkcs12), null);
    }

    private static string GuardarPem(X509Certificate2 ca)
    {
        string ruta = Path.Combine(Path.GetTempPath(), $"ca-interna-{Guid.NewGuid():N}.pem");
        File.WriteAllText(ruta, ca.ExportCertificatePem());
        return ruta;
    }

    private static async Task<ServicioHttps> IniciarServicioAsync(X509Certificate2 certificado)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(opciones =>
            opciones.Listen(IPAddress.Loopback, 0, escucha => escucha.UseHttps(certificado)));
        var app = builder.Build();
        app.MapGet("/salud", () => Results.Ok());
        await app.StartAsync();
        string direccion = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        return new ServicioHttps(app, new Uri(direccion).Port, certificado);
    }

    private sealed class ServicioHttps(WebApplication app, int puerto, X509Certificate2 certificado) : IAsyncDisposable
    {
        public string Url(string nombre) => $"https://{nombre}:{puerto}/salud";

        public async ValueTask DisposeAsync()
        {
            await app.DisposeAsync();
            certificado.Dispose();
        }
    }
}
