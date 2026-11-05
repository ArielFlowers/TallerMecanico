using TallerMecanico.Dominio.Entidades;

namespace TallerMecanico.Aplicacion.Dto;

public class ServicioTablaViewModel
{
    public IReadOnlyList<Servicio> Servicios { get; init; } = [];

    public bool MostrarAcciones { get; init; }
}
