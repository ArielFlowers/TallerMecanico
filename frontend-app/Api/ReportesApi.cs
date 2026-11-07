namespace TallerMecanico.Api;

// Cliente del Servicio 3: reportes.
// Las rutas están en la tabla "Contrato de las APIs" del plan.
public sealed class ReportesApi(HttpClient http) : ApiBase(http)
{
    public Task<DocumentoReporteOrdenes> ObtenerOrdenesAsync(
        FiltroReporteOrdenes filtro, CancellationToken cancelacion = default) =>
        LeerAsync<DocumentoReporteOrdenes>(ConFiltros("api/reportes/ordenes",
            ("fechaDesde", filtro.FechaDesde),
            ("fechaHasta", filtro.FechaHasta),
            ("estado", filtro.Estado)), cancelacion);

    public Task<byte[]> ExportarOrdenesPdfAsync(
        FiltroReporteOrdenes filtro, CancellationToken cancelacion = default) =>
        LeerArchivoAsync(ConFiltros("api/reportes/ordenes/pdf",
            ("fechaDesde", filtro.FechaDesde),
            ("fechaHasta", filtro.FechaHasta),
            ("estado", filtro.Estado)), cancelacion);

    public Task<byte[]> ExportarOrdenesExcelAsync(
        FiltroReporteOrdenes filtro, CancellationToken cancelacion = default) =>
        LeerArchivoAsync(ConFiltros("api/reportes/ordenes/excel",
            ("fechaDesde", filtro.FechaDesde),
            ("fechaHasta", filtro.FechaHasta),
            ("estado", filtro.Estado)), cancelacion);

    public Task<DocumentoReporteMecanicos> ObtenerMecanicosAsync(
        FiltroReporteMecanicos filtro, CancellationToken cancelacion = default) =>
        LeerAsync<DocumentoReporteMecanicos>(ConFiltros("api/reportes/mecanicos",
            ("nombreMecanico", filtro.NombreMecanico),
            ("fechaDesde", filtro.FechaDesde),
            ("fechaHasta", filtro.FechaHasta),
            ("mecanicoId", filtro.MecanicoId)), cancelacion);

    public Task<byte[]> ExportarMecanicosPdfAsync(
        FiltroReporteMecanicos filtro, CancellationToken cancelacion = default) =>
        LeerArchivoAsync(ConFiltros("api/reportes/mecanicos/pdf",
            ("nombreMecanico", filtro.NombreMecanico),
            ("fechaDesde", filtro.FechaDesde),
            ("fechaHasta", filtro.FechaHasta),
            ("mecanicoId", filtro.MecanicoId)), cancelacion);

    public Task<byte[]> ExportarMecanicosExcelAsync(
        FiltroReporteMecanicos filtro, CancellationToken cancelacion = default) =>
        LeerArchivoAsync(ConFiltros("api/reportes/mecanicos/excel",
            ("nombreMecanico", filtro.NombreMecanico),
            ("fechaDesde", filtro.FechaDesde),
            ("fechaHasta", filtro.FechaHasta),
            ("mecanicoId", filtro.MecanicoId)), cancelacion);
}
