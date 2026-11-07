using TallerMecanico.Contratos.Ordenes;

namespace TallerMecanico.Api;

// Cliente del Servicio 1: órdenes y dashboard.
// Las rutas están en la tabla "Contrato de las APIs" del plan.
public sealed class OrdenesApi(HttpClient http) : ApiBase(http)
{
    public Task<IReadOnlyList<OrdenResumenDto>> ObtenerAsync(EstadoOrden? estado, DateOnly? fecha,
        string? terminoBusqueda, int? ordenSeleccionadaId, CancellationToken cancelacion = default) =>
        LeerAsync<IReadOnlyList<OrdenResumenDto>>(ConFiltros("api/ordenes", ("estado", estado), ("fecha", fecha),
            ("terminoBusqueda", terminoBusqueda), ("ordenSeleccionadaId", ordenSeleccionadaId)), cancelacion);

    public Task<IReadOnlyList<SugerenciaOrden>> SugerirAsync(string? termino, EstadoOrden? estado,
        DateOnly? fecha, CancellationToken cancelacion = default) =>
        LeerAsync<IReadOnlyList<SugerenciaOrden>>(ConFiltros("api/ordenes/sugerencias", ("termino", termino),
            ("estado", estado), ("fecha", fecha)), cancelacion);

    public Task<OrdenDetalleDto?> ObtenerDetalleAsync(int id, CancellationToken cancelacion = default) =>
        LeerOpcionalAsync<OrdenDetalleDto>($"api/ordenes/{id}", cancelacion);

    public Task<byte[]?> GenerarComprobanteAsync(int id) => LeerArchivoOpcionalAsync($"api/ordenes/{id}/comprobante");

    public Task<DateTime> ObtenerFechaActualAsync(CancellationToken cancelacion = default) =>
        LeerAsync<DateTime>("api/ordenes/fecha-actual", cancelacion);

    public Task<ResultadoOrdenDto> CrearAsync(CrearOrdenDto solicitud) =>
        GuardarAsync<ResultadoOrdenDto>(HttpMethod.Post, "api/ordenes", solicitud);

    public Task<ResultadoOrdenDto> AnularAsync(int id) =>
        GuardarAsync<ResultadoOrdenDto>(HttpMethod.Post, $"api/ordenes/{id}/anular");

    public Task<DashboardViewModel> ObtenerDashboardAsync(CancellationToken cancelacion = default) =>
        LeerAsync<DashboardViewModel>("api/dashboard", cancelacion);
}
