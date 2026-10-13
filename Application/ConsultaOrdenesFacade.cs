using TallerMecanico.Application.Ordenes;
using TallerMecanico.Models;
using TallerMecanico.Services;

namespace TallerMecanico.Application;

public sealed class ConsultaOrdenesFacade(ConsultaOrdenesService servicio)
{
    public IReadOnlyList<OrdenResumen> Obtener(
        EstadoOrden? estado = null,
        DateOnly? fecha = null) => servicio.Obtener(estado, fecha);
}
