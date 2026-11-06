using TallerMecanico.Contratos.Catalogos;

namespace TallerMecanico.Aplicacion.Dto;

public class ServicioTablaViewModel
{
    public IReadOnlyList<ServicioDto> Servicios { get; init; } = [];

    public bool MostrarAcciones { get; init; }
}
