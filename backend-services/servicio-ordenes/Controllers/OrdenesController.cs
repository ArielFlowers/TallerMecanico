using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TallerMecanico.Aplicacion.Fachadas;
using TallerMecanico.Contratos.Catalogos;
using TallerMecanico.Contratos.Ordenes;
using EstadoOrdenDominio = TallerMecanico.Dominio.Entidades.EstadoOrden;
using TallerMecanico.Contratos;

namespace TallerMecanico.Controllers;

[ApiController]
[Route("api/ordenes")]
[Authorize(Roles = Roles.Personal)]
public sealed class OrdenesController(
    ConsultaOrdenesFacade consultas,
    OrdenServicioFacade ordenes) : ControllerBase
{
    [HttpGet]
    public IReadOnlyList<OrdenResumenDto> Obtener(
        [FromQuery] EstadoOrden? estado = null,
        [FromQuery] DateOnly? fecha = null,
        [FromQuery] string? terminoBusqueda = null,
        [FromQuery] int? ordenSeleccionadaId = null) =>
        consultas.Obtener(
                estado is null ? null : (EstadoOrdenDominio)estado.Value,
                fecha,
                terminoBusqueda,
                ordenSeleccionadaId)
            .Select(orden => orden.ADto())
            .ToArray();

    [HttpGet("sugerencias")]
    public Task<IReadOnlyList<SugerenciaOrden>> Sugerir(
        [FromQuery] string? termino,
        [FromQuery] EstadoOrden? estado,
        [FromQuery] DateOnly? fecha,
        CancellationToken cancelacion) =>
        consultas.SugerirAsync(
            termino,
            estado is null ? null : (EstadoOrdenDominio)estado.Value,
            fecha,
            cancelacion);

    [HttpGet("fecha-actual")]
    public DateTime FechaActual() => ordenes.FechaActual;

    [HttpGet("{id:int}")]
    public ActionResult<OrdenDetalleDto> ObtenerPorId(int id)
    {
        var detalle = consultas.ObtenerDetalle(id);
        return detalle is null ? NotFound() : Ok(detalle.ADto());
    }

    [HttpGet("{id:int}/comprobante")]
    public IActionResult Comprobante(int id)
    {
        byte[]? pdf = consultas.GenerarComprobante(id);
        return pdf is null ? NotFound() : File(pdf, "application/pdf");
    }

    [HttpPost]
    public ResultadoOrdenDto Crear([FromBody] CrearOrdenDto orden) =>
        ordenes.CrearOrden(orden.ASolicitud()).ADto();

    [HttpPost("{id:int}/anular")]
    public ResultadoOrdenDto Anular(int id) => ordenes.AnularOrden(id).ADto();
}
