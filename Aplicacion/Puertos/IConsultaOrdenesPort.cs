using TallerMecanico.Aplicacion.CasosUso.Ordenes;
using TallerMecanico.Dominio.Entidades;

namespace TallerMecanico.Aplicacion.Puertos;

public interface IConsultaOrdenesPort
{
    IReadOnlyList<OrdenResumen> Obtener(
        EstadoOrden? estado = null,
        DateOnly? fecha = null);

    OrdenDetalle? ObtenerDetalle(int id);
}
