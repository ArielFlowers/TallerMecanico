
namespace TallerMecanico.Aplicacion.Puertos;

public interface IPasswordHasher
{
    string GenerarHash(string password);

    bool VerificarPassword(
        string password,
        string passwordHash);
}
