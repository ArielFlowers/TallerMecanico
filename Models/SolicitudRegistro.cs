namespace TallerMecanico.Models;

public class SolicitudRegistro
{
    public long Id { get; set; }

    public string Username { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string PasswordHash { get; set; } = string.Empty;

    public string Estado { get; set; } = "PendienteVerificacion";

    public string? TokenVerificacionHash { get; set; }

    public DateTime? TokenExpiraEn { get; set; }

    public DateTime? EmailVerificadoEn { get; set; }

    public DateTime FechaSolicitud { get; set; }

    public DateTime? UltimoIntentoVerificacionEn { get; set; }

    public DateTime? FechaResolucion { get; set; }

    public int? RevisadoPorUsuarioId { get; set; }
}