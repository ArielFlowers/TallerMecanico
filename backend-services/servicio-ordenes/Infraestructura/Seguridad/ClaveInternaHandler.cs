using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace TallerMecanico.Infraestructura.Seguridad;

// Solo acepta llamadas con la clave interna que comparten el Frontend y las APIs.
// Las cabeceras X-Usuario-* indican qué usuario inició sesión en el Frontend.
public sealed class ClaveInternaHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> opciones,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IConfiguration configuracion) : AuthenticationHandler<AuthenticationSchemeOptions>(opciones, logger, encoder)
{
    public const string Esquema = "ClaveInterna";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        string clave = configuracion["ClaveInterna"]
            ?? throw new InvalidOperationException("Configura ClaveInterna para esta API.");
        string recibida = Request.Headers["X-Clave-Interna"].ToString();

        if (!CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(recibida), Encoding.UTF8.GetBytes(clave)))
            return Task.FromResult(AuthenticateResult.Fail("Clave interna incorrecta."));

        var claims = new List<Claim>();
        AgregarClaim(claims, ClaimTypes.NameIdentifier, "X-Usuario-Id");
        AgregarClaim(claims, ClaimTypes.Name, "X-Usuario-Nombre");
        AgregarClaim(claims, ClaimTypes.Role, "X-Usuario-Rol");

        var identidad = new ClaimsIdentity(claims, Esquema);
        return Task.FromResult(AuthenticateResult.Success(
            new AuthenticationTicket(new ClaimsPrincipal(identidad), Esquema)));
    }

    private void AgregarClaim(List<Claim> claims, string tipo, string cabecera)
    {
        string valor = Request.Headers[cabecera].ToString();
        if (!string.IsNullOrEmpty(valor))
            claims.Add(new Claim(tipo, Uri.UnescapeDataString(valor)));
    }
}
