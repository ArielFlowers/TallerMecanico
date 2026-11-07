namespace TallerMecanico.Contratos.Usuarios;

public sealed record UsuarioListadoDto(
    int Id,
    string Username,
    string Email,
    string Rol,
    bool Activo,
    DateTime FechaCreacion);