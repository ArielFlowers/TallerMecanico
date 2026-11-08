using TallerMecanico.Contratos.Catalogos;

namespace TallerMecanico.Pages.Servicios;

public class ServicioTablaViewModel
{
    public IReadOnlyList<ServicioDto> Servicios { get; init; } = [];

    public bool MostrarAcciones { get; init; }
}
