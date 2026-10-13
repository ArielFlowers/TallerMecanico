using TallerMecanico.Aplicacion.CasosUso.Ordenes;
using TallerMecanico.Dominio.Entidades;
using TallerMecanico.Aplicacion.Servicios;

namespace TallerMecanico.Aplicacion.Fachadas;

public sealed class ConsultaOrdenesFacade(ConsultaOrdenesService servicio, ComprobanteOrdenService comprobantes)
{
    public IReadOnlyList<OrdenResumen> Obtener(
        EstadoOrden? estado = null,
        DateOnly? fecha = null) => servicio.Obtener(estado, fecha);

    public OrdenDetalle? ObtenerDetalle(int id) => servicio.ObtenerDetalle(id);

    public byte[]? GenerarComprobante(int id) => comprobantes.Generar(id);
}
