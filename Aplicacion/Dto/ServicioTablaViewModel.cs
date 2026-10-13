using TallerMecanico.Models;

namespace TallerMecanico.ViewModels;

public class ServicioTablaViewModel
{
    public IReadOnlyList<Servicio> Servicios { get; init; } = [];

    public bool MostrarAcciones { get; init; }
}
