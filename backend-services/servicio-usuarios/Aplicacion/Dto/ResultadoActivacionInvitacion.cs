namespace TallerMecanico.Aplicacion.Dto;

public sealed record ResultadoActivacionInvitacion(
    bool Exito,
    string? Username,
    string? Rol,
    string? MensajeError);