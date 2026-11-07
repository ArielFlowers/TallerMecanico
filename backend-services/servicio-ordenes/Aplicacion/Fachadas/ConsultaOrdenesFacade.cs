using TallerMecanico.Contratos.Catalogos;
using TallerMecanico.Aplicacion.CasosUso.Ordenes;
using TallerMecanico.Dominio.Entidades;

namespace TallerMecanico.Aplicacion.Fachadas;

public sealed class ConsultaOrdenesFacade(ConsultarOrdenesCasoUso consultarOrdenes, GenerarComprobanteCasoUso comprobantes)
{
    public IReadOnlyList<OrdenResumen> Obtener(
        EstadoOrden? estado = null,
        DateOnly? fecha = null,
        string? terminoBusqueda = null,
        int? ordenSeleccionadaId = null) =>
        consultarOrdenes.Obtener(estado, fecha, terminoBusqueda, ordenSeleccionadaId);

    public Task<IReadOnlyList<SugerenciaOrden>> SugerirAsync(
        string? termino,
        EstadoOrden? estado = null,
        DateOnly? fecha = null,
        CancellationToken cancellationToken = default) =>
        consultarOrdenes.SugerirAsync(termino, estado, fecha, cancellationToken);

    public OrdenDetalle? ObtenerDetalle(int id) => consultarOrdenes.ObtenerDetalle(id);

    public byte[]? GenerarComprobante(int id) => comprobantes.Generar(id);
}
