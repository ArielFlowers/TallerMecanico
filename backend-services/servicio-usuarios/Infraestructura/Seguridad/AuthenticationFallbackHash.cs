
using TallerMecanico.Aplicacion.Puertos;

namespace TallerMecanico.Infraestructura.Seguridad;

public sealed class AuthenticationFallbackHash : IFallbackHash
{
    public string Hash { get; }

    public AuthenticationFallbackHash(
        IPasswordHasher passwordHasher)
    {
        string passwordFicticio =
            Guid.NewGuid().ToString("N");

        Hash = passwordHasher.GenerarHash(
            passwordFicticio);
    }
}
