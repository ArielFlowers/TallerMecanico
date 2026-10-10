
namespace TallerMecanico.Models;

public sealed class RegistroAuditoria
{
    public long Id { get; set; }

    public int? UsuarioId { get; set; }

    public string? Username { get; set; }

    public string Accion { get; set; } = string.Empty;

    public string Entidad { get; set; } = string.Empty;

    public string? EntidadId { get; set; }

    public DateTime Fecha { get; set; } = DateTime.UtcNow;
}
