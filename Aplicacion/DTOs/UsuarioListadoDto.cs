namespace TallerMecanico.Aplicacion.DTOs;

public sealed record UsuarioListadoDto(
    int Id,
    string Username,
    string Email,
    string Rol,
    bool Activo,
    DateTime FechaCreacion);