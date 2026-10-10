
namespace TallerMecanico.Application.Ports;

public interface ICurrentUser
{
    bool EstaAutenticado { get; }

    int? UsuarioId { get; }

    string? Username { get; }

    string? Rol { get; }
}
