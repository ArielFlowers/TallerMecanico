namespace TallerMecanico.Models;

public class Mecanico
{
    public int Id { get; set; }

    public string Ci { get; set; } = string.Empty;

    public string NombreCompleto { get; set; } = string.Empty;

    public string Especialidad { get; set; } = string.Empty;

    public string Celular { get; set; } = string.Empty;
}
