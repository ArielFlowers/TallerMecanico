namespace TallerMecanico.Contratos.Usuarios;

public sealed record UsuarioListadoDto(
    int Id,
    string Username,
    string Email,
    string Rol,
    bool Activo,
    DateTime FechaCreacion,
    // Minutos que le quedan a la sesión abierta; null si no tiene sesión (RF-07).
    int? MinutosSesionRestantes = null);