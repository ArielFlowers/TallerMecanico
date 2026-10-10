
namespace TallerMecanico.Application.Ports;

public interface IPasswordHasher
{
    string GenerarHash(string password);

    bool VerificarPassword(
        string password,
        string passwordHash);
}
