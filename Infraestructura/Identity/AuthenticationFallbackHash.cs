
using TallerMecanico.Application.Ports;

namespace TallerMecanico.Infraestructura.Identity;

public sealed class AuthenticationFallbackHash
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
