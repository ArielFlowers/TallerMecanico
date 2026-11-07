using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TallerMecanico.Aplicacion.CasosUso.Ordenes;
using TallerMecanico.Contratos.Ordenes;
using EstadoOrden = TallerMecanico.Dominio.Entidades.EstadoOrden;

namespace TallerMecanico.Controllers;

[ApiController]
[Route("api/ordenes")]
[Authorize(Roles = "Administrador,Recepcionista")]
public sealed class OrdenesController(
    ConsultarOrdenesCasoUso consultas,
    CrearOrdenCasoUso crear,
    AnularOrdenCasoUso anular,
    GenerarComprobanteCasoUso comprobantes) : ControllerBase
{
    [HttpGet]
    public IEnumerable<OrdenResumenDto> Obtener(
        EstadoOrden? estado, DateOnly? fecha, string? terminoBusqueda, int? ordenSeleccionadaId) =>
        consultas.Obtener(estado, fecha, terminoBusqueda, ordenSeleccionadaId).Select(orden => orden.ADto());

    [HttpGet("sugerencias")]
    public Task<IReadOnlyList<TallerMecanico.Contratos.Catalogos.SugerenciaOrden>> Sugerir(
        string? termino, EstadoOrden? estado, DateOnly? fecha, CancellationToken cancelacion) =>
        consultas.SugerirAsync(termino, estado, fecha, cancelacion);

    [HttpGet("fecha-actual")]
    public DateTime FechaActual() => crear.FechaActual;

    [HttpGet("{id:int}")]
    public ActionResult<OrdenDetalleDto> ObtenerDetalle(int id) =>
        consultas.ObtenerDetalle(id) is { } orden ? Ok(orden.ADto()) : NotFound();

    [HttpGet("{id:int}/comprobante")]
    public IActionResult Comprobante(int id) => comprobantes.Generar(id) is { } pdf
        ? File(pdf, "application/pdf", $"Comprobante-Orden-{id}.pdf")
        : NotFound();

    [HttpPost]
    public ActionResult<ResultadoOrdenDto> Crear(CrearOrdenDto solicitud)
    {
        if (solicitud.Lineas is null || solicitud.ServicioIds is null || solicitud.CantidadesServicios is null)
            return BadRequest(new TallerMecanico.Contratos.ErrorApi("Revisa los servicios y repuestos de la orden."));
        return crear.Ejecutar(solicitud.ASolicitud()).ADto();
    }

    [HttpPost("{id:int}/anular")]
    [Authorize(Roles = "Administrador")]
    public ResultadoOrdenDto Anular(int id) => anular.Ejecutar(id).ADto();
}
