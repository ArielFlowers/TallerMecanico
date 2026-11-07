using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

namespace TallerMecanico.Api;

// Registra un cliente HTTP por cada servicio backend.
public static class RegistroApis
{
    public static IServiceCollection AddApisInternas(this IServiceCollection servicios, IConfiguration configuracion)
    {
        servicios.AddTransient<CabecerasInternasHandler>();
        string? certificadoCa = configuracion["Servicios:CertificadoCa"];
        Registrar<OrdenesApi>(servicios, configuracion, "Servicios:OrdenesUrl", certificadoCa);
        Registrar<ClientesApi>(servicios, configuracion, "Servicios:OrdenesUrl", certificadoCa);
        Registrar<VehiculosApi>(servicios, configuracion, "Servicios:OrdenesUrl", certificadoCa);
        Registrar<ProductosApi>(servicios, configuracion, "Servicios:OrdenesUrl", certificadoCa);
        Registrar<MecanicosApi>(servicios, configuracion, "Servicios:OrdenesUrl", certificadoCa);
        Registrar<ServiciosApi>(servicios, configuracion, "Servicios:OrdenesUrl", certificadoCa);
        Registrar<UsuariosApi>(servicios, configuracion, "Servicios:UsuariosUrl", certificadoCa);
        Registrar<ReportesApi>(servicios, configuracion, "Servicios:ReportesUrl", certificadoCa);
        return servicios;
    }

    // HTTPS interno: con una CA configurada, el Frontend solo acepta certificados firmados por ella.
    // El nombre del servidor se sigue validando; nunca se desactiva la validación del certificado.
    public static HttpMessageHandler CrearManejador(string? certificadoCa)
    {
        var manejador = new SocketsHttpHandler();
        if (string.IsNullOrWhiteSpace(certificadoCa)) return manejador;

        var cadena = new X509ChainPolicy
        {
            TrustMode = X509ChainTrustMode.CustomRootTrust,
            RevocationMode = X509RevocationMode.NoCheck
        };
        cadena.CustomTrustStore.Add(X509CertificateLoader.LoadCertificateFromFile(certificadoCa));
        manejador.SslOptions = new SslClientAuthenticationOptions { CertificateChainPolicy = cadena };
        return manejador;
    }

    private static void Registrar<TApi>(IServiceCollection servicios, IConfiguration configuracion, string claveUrl,
        string? certificadoCa) where TApi : class =>
        servicios.AddHttpClient<TApi>(cliente => cliente.BaseAddress = new Uri(configuracion[claveUrl]
                ?? throw new InvalidOperationException($"Configura {claveUrl} en el Frontend.")))
            .ConfigurePrimaryHttpMessageHandler(() => CrearManejador(certificadoCa))
            .AddHttpMessageHandler<CabecerasInternasHandler>();
}
