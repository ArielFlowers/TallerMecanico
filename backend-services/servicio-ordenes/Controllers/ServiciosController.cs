using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TallerMecanico.Aplicacion.Servicios;
using TallerMecanico.Contratos;
using TallerMecanico.Contratos.Catalogos;
using TallerMecanico.Dominio.Entidades;

namespace TallerMecanico.Controllers;

[ApiController]
[Route("api/servicios")]
[Authorize(Roles = "Administrador,Recepcionista")]
public sealed class ServiciosController(
    IServicioService servicios,
    IHistorialCostoServicioService historial) : ControllerBase
{
    // Se validan los campos antes del filtro automático de ApiController (orden -2000).
    private const int OrdenValidacionFormulario = -3000;

    [HttpGet]
    public async Task<IReadOnlyList<ServicioDto>> Obtener([FromQuery] string? terminoBusqueda, CancellationToken cancelacion)
    {
        var encontrados = string.IsNullOrWhiteSpace(terminoBusqueda)
            ? servicios.ObtenerTodos()
            : await servicios.BuscarAsync(terminoBusqueda, cancelacion);
        return encontrados.Select(servicio => servicio.ADto()).ToArray();
    }

    [HttpGet("sugerencias")]
    public Task<IReadOnlyList<SugerenciaServicio>> Sugerir([FromQuery] string? termino, CancellationToken cancelacion) =>
        servicios.SugerirAsync(termino, cancelacion);

    [HttpGet("catalogo")]
    public IReadOnlyList<ServicioDto> ObtenerCatalogo() => servicios.ObtenerCatalogo().Select(servicio => servicio.ADto()).ToArray();

    [HttpGet("historial")]
    [Authorize(Roles = "Administrador")]
    public IReadOnlyList<HistorialCostoServicioDto> ObtenerHistorial() => historial.ObtenerHistorial().Select(cambio => cambio.ADto()).ToArray();

    [HttpGet("{id:int}")]
    public ActionResult<ServicioDto> ObtenerPorId(int id)
    {
        var servicio = servicios.ObtenerPorId(id);
        return servicio is null ? NotFound() : Ok(servicio.ADto());
    }

    [HttpPost]
    [Authorize(Roles = "Administrador")]
    [TypeFilter(typeof(ValidacionFormularioServicioFilter), Order = OrdenValidacionFormulario)]
    public ActionResult<ResultadoFormulario<ServicioFormViewModel>> Crear([FromBody] ServicioFormViewModel formulario) =>
        Guardar(formulario);

    [HttpPut("{id:int}")]
    [Authorize(Roles = "Administrador")]
    [TypeFilter(typeof(ValidacionFormularioServicioFilter), Order = OrdenValidacionFormulario)]
    public ActionResult<ResultadoFormulario<ServicioFormViewModel>> Actualizar(int id, [FromBody] ServicioFormViewModel formulario) =>
        Guardar(formulario, id);

    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Administrador")]
    public IActionResult Eliminar(int id)
    {
        try { servicios.Eliminar(id); }
        catch (InvalidOperationException error) { return Conflict(new ErrorApi(error.Message)); }
        return NoContent();
    }

    private ActionResult<ResultadoFormulario<ServicioFormViewModel>> Guardar(ServicioFormViewModel formulario, int? id = null)
    {
        var servicio = new Servicio
        {
            Id = id ?? 0,
            Nombre = formulario.Nombre,
            Descripcion = formulario.Descripcion,
            Costo = formulario.Costo,
            TiempoEstimadoHoras = formulario.TiempoEstimadoHoras,
            UnidadCantidad = string.IsNullOrWhiteSpace(formulario.UnidadCantidad) ? null : formulario.UnidadCantidad.Trim()
        };
        try
        {
            if (id.HasValue) servicios.Actualizar(servicio);
            else servicios.Crear(servicio);
        }
        catch (ArgumentException error) { return BadRequest(new ErrorApi(error.Message)); }
        catch (InvalidOperationException error) when (id.HasValue) { return Conflict(new ErrorApi(error.Message)); }

        formulario.UnidadCantidad = servicio.UnidadCantidad;
        return Ok(new ResultadoFormulario<ServicioFormViewModel>(formulario, new Dictionary<string, string>()));
    }
}
