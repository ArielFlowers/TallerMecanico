using TallerMecanico.Aplicacion.Errores;

namespace TallerMecanico.Aplicacion.Servicios;

public static class EnlaceVerificacion
{
    public static Uri ObtenerBase(IConfiguration configuration)
    {
        const string clave = "App:PublicBaseUrl";
        const string claveHttpNoSeguro = "App:PermitirHttpNoSeguro";

        bool permiteHttpNoSeguro =
            bool.TryParse(configuration[claveHttpNoSeguro], out bool permitido) &&
            permitido;

        if (!Uri.TryCreate(configuration[clave], UriKind.Absolute, out Uri? uri) ||
            (uri.Scheme != Uri.UriSchemeHttps &&
             !(uri.Scheme == Uri.UriSchemeHttp &&
               (uri.IsLoopback || permiteHttpNoSeguro))) ||
            !string.IsNullOrEmpty(uri.UserInfo) ||
            !string.IsNullOrEmpty(uri.Query) ||
            !string.IsNullOrEmpty(uri.Fragment))
        {
            throw new ConfiguracionRegistroException(clave);
        }

        return uri;
    }

    public static string CrearVerificacion(Uri uriBase, string token)
    {
        return new Uri(
            uriBase,
            "/Auth/VerificarEmail?token=" + Uri.EscapeDataString(token))
            .AbsoluteUri;
    }

    public static string CrearActivacionInvitacion(Uri uriBase, string token)
    {
        return new Uri(
            uriBase,
            "/Auth/ActivarInvitacion?token=" + Uri.EscapeDataString(token))
            .AbsoluteUri;
    }
}
