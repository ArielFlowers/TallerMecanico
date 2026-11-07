
namespace TallerMecanico.Dominio.Entidades;

public sealed record UsuarioAutenticado(
    int Id,
    string Username,
    string Rol);
