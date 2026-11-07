using System.Security.Claims;

namespace TallerMecanico.Api;

// Añade a cada llamada la clave interna y el usuario que inició sesión.
public sealed class CabecerasInternasHandler(
    IHttpContextAccessor contexto,
    IConfiguration configuracion) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage solicitud, CancellationToken cancelacion)
    {
        solicitud.Headers.Add("X-Clave-Interna", configuracion["ClaveInterna"]
            ?? throw new InvalidOperationException("Configura ClaveInterna en el Frontend."));

        ClaimsPrincipal? usuario = contexto.HttpContext?.User;
        if (usuario?.Identity?.IsAuthenticated == true)
        {
            AgregarCabecera(solicitud, "X-Usuario-Id", usuario.FindFirstValue(ClaimTypes.NameIdentifier));
            AgregarCabecera(solicitud, "X-Usuario-Nombre", usuario.FindFirstValue(ClaimTypes.Name));
            AgregarCabecera(solicitud, "X-Usuario-Rol", usuario.FindFirstValue(ClaimTypes.Role));
        }
        return base.SendAsync(solicitud, cancelacion);
    }

    private static void AgregarCabecera(HttpRequestMessage solicitud, string nombre, string? valor)
    {
        if (!string.IsNullOrEmpty(valor))
            solicitud.Headers.Add(nombre, Uri.EscapeDataString(valor));
    }
}
