namespace TallerMecanico.Aplicacion.DTOs;

public sealed record ResultadoActivacionInvitacion(
    bool Exito,
    string? Username,
    string? Rol,
    string? MensajeError);