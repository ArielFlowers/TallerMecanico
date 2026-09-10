namespace TallerMecanico.Models;

public class Vehiculo
{
    public int Id { get; set; }

    public string Placa { get; set; } = string.Empty;

    public string Modelo { get; set; } = string.Empty;

    public int Kilometraje { get; set; }

    public string Observaciones { get; set; } = string.Empty;
}
