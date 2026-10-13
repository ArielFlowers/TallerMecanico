namespace TallerMecanico.Models;

public class Cliente
{
    public int Id { get; set; }

    public string Ci { get; set; } = string.Empty;

    public string ComplementoCi { get; set; } = string.Empty;

    public string Nombres { get; set; } = string.Empty;

    public string PrimerApellido { get; set; } = string.Empty;

    public string SegundoApellido { get; set; } = string.Empty;

    public string Celular { get; set; } = string.Empty;

    public string CreadoPor { get; set; } = string.Empty;

    public DateTime FechaCreacion { get; set; }
}