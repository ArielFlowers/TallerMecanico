
namespace TallerMecanico.Models;

public class Usuario
{
    public int Id { get; set; }

    public string Username { get; set; } = string.Empty;

    public string PasswordHash { get; set; } = string.Empty;

    public string? Email { get; set; }

    public bool EmailVerificado { get; set; } = false;

    public string Rol { get; set; } = string.Empty;

    public bool Activo { get; set; } = true;

    public string CreadoPor { get; set; } = string.Empty;

    public DateTime FechaCreacion { get; set; }

    public string? ModificadoPor { get; set; }

    public DateTime? FechaModificacion { get; set; }
}
