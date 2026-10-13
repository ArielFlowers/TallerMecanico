using TallerMecanico.Aplicacion.Errores;

namespace TallerMecanico.Aplicacion.Servicios;

public static class EnlaceVerificacion
{
    public static Uri ObtenerBase(IConfiguration configuration)
    {
        const string clave = "App:PublicBaseUrl";

        if (!Uri.TryCreate(configuration[clave], UriKind.Absolute, out Uri? uri) ||
            (uri.Scheme != Uri.UriSchemeHttps &&
             !(uri.Scheme == Uri.UriSchemeHttp && uri.IsLoopback)) ||
            !string.IsNullOrEmpty(uri.UserInfo) ||
            !string.IsNullOrEmpty(uri.Query) ||
            !string.IsNullOrEmpty(uri.Fragment))
        {
            throw new ConfiguracionRegistroException(clave);
        }

        return uri;
    }

    public static string Crear(Uri uriBase, string token)
    {
        return new Uri(
            uriBase,
            "/Auth/VerificarEmail?token=" + Uri.EscapeDataString(token))
            .AbsoluteUri;
    }
}
