using TallerMecanico.Application.Ordenes;
using TallerMecanico.Models;

namespace TallerMecanico.Application.Ports;

public interface IConsultaOrdenesPort
{
    IReadOnlyList<OrdenResumen> Obtener(
        EstadoOrden? estado = null,
        DateOnly? fecha = null);
}
