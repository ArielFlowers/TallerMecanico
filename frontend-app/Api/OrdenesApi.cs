using TallerMecanico.Contratos.Dashboard;
using TallerMecanico.Contratos.Ordenes;

namespace TallerMecanico.Api;

// Cliente del Servicio 1: órdenes y dashboard.
// Las rutas están en la tabla "Contrato de las APIs" del plan.
public sealed class OrdenesApi(HttpClient http) : ApiBase(http)
{
    public Task<IReadOnlyList<OrdenResumenDto>> ObtenerAsync(
        EstadoOrden? estado = null,
        DateOnly? fecha = null,
        string? terminoBusqueda = null,
        int? ordenSeleccionadaId = null,
        CancellationToken cancelacion = default) =>
        LeerAsync<IReadOnlyList<OrdenResumenDto>>(ConFiltros("api/ordenes",
            ("estado", estado), ("fecha", fecha), ("terminoBusqueda", terminoBusqueda),
            ("ordenSeleccionadaId", ordenSeleccionadaId)), cancelacion);

    public Task<IReadOnlyList<SugerenciaOrden>> SugerirAsync(
        string? termino,
        EstadoOrden? estado = null,
        DateOnly? fecha = null,
        CancellationToken cancelacion = default) =>
        LeerAsync<IReadOnlyList<SugerenciaOrden>>(ConFiltros("api/ordenes/sugerencias",
            ("termino", termino), ("estado", estado), ("fecha", fecha)), cancelacion);

    public Task<DateTime> ObtenerFechaActualAsync(CancellationToken cancelacion = default) =>
        LeerAsync<DateTime>("api/ordenes/fecha-actual", cancelacion);

    public Task<OrdenDetalleDto?> ObtenerPorIdAsync(int id, CancellationToken cancelacion = default) =>
        LeerOpcionalAsync<OrdenDetalleDto>($"api/ordenes/{id}", cancelacion);

    public Task<byte[]?> ObtenerComprobanteAsync(int id) =>
        LeerArchivoOpcionalAsync($"api/ordenes/{id}/comprobante");

    public Task<ResultadoOrdenDto> CrearAsync(CrearOrdenDto orden) =>
        GuardarAsync<ResultadoOrdenDto>(HttpMethod.Post, "api/ordenes", orden);

    public Task<ResultadoOrdenDto> AnularAsync(int id) =>
        GuardarAsync<ResultadoOrdenDto>(HttpMethod.Post, $"api/ordenes/{id}/anular");

    public Task<DashboardViewModel> ObtenerDashboardAsync(CancellationToken cancelacion = default) =>
        LeerAsync<DashboardViewModel>("api/dashboard", cancelacion);
}
