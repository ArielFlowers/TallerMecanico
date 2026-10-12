using System.Text.Json;
using TallerMecanico.Application.Ports;

namespace TallerMecanico.Infraestructura.Session;

public sealed class TokenOrdenSesion(IHttpContextAccessor contexto, ICurrentUser usuario, TimeProvider reloj) : ITokenOrdenPort
{
    private sealed record TokenFormulario(string Usuario, DateTime VenceUtc);
    private ISession Session => contexto.HttpContext?.Session ?? throw new InvalidOperationException("No hay sesión disponible.");
    private static string Clave(Guid token) => $"Ordenes:Token:{token:N}";

    public Guid Generar()
    {
        if (!usuario.EstaAutenticado || string.IsNullOrWhiteSpace(usuario.Username))
            throw new UnauthorizedAccessException("Debes iniciar sesión.");
        Guid token = Guid.NewGuid();
        var datos = new TokenFormulario(usuario.Username, reloj.GetUtcNow().UtcDateTime.AddMinutes(30));
        Session.SetString(Clave(token), JsonSerializer.Serialize(datos));
        return token;
    }

    public bool EsValido(Guid token)
    {
        if (token == Guid.Empty || !usuario.EstaAutenticado) return false;
        string? contenido = Session.GetString(Clave(token));
        if (contenido is null) return false;
        TokenFormulario? datos = JsonSerializer.Deserialize<TokenFormulario>(contenido);
        if (datos is null || datos.Usuario != usuario.Username || datos.VenceUtc <= reloj.GetUtcNow().UtcDateTime)
        {
            Session.Remove(Clave(token));
            return false;
        }
        // Se conserva tras confirmar para que un doble clic/reintento pueda recuperar la orden.
        return true;
    }
}
