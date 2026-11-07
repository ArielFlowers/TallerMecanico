using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TallerMecanico.Aplicacion.Servicios;
using TallerMecanico.Contratos;
using TallerMecanico.Contratos.Catalogos;

namespace TallerMecanico.Controllers;

[ApiController]
[Route("api/mecanicos")]
[Authorize(Roles = "Administrador,Recepcionista")]
public sealed class MecanicosController(MecanicoService mecanicos) : ControllerBase
{
    [HttpGet]
    public IReadOnlyList<MecanicoDto> Obtener([FromQuery] string? terminoBusqueda = null) =>
        mecanicos.Obtener(terminoBusqueda).Select(mecanico => mecanico.ADto()).ToArray();

    [HttpGet("sugerencias")]
    public Task<IReadOnlyList<SugerenciaMecanico>> Sugerir([FromQuery] string? termino, CancellationToken cancelacion,
        [FromQuery] bool incluirEspecialidad = true) =>
        mecanicos.SugerirAsync(termino, cancelacion, incluirEspecialidad);

    [HttpGet("{id:int}")]
    public ActionResult<MecanicoDto> ObtenerPorId(int id)
    {
        var mecanico = mecanicos.ObtenerPorId(id).FirstOrDefault();
        return mecanico is null ? NotFound() : Ok(mecanico.ADto());
    }

    [HttpPost]
    [Authorize(Roles = "Administrador")]
    public IReadOnlyDictionary<string, string> Crear([FromBody] MecanicoInputModel formulario) => mecanicos.Crear(formulario);

    [HttpPut("{id:int}")]
    [Authorize(Roles = "Administrador")]
    public ResultadoMecanico Actualizar(int id, [FromBody] MecanicoInputModel formulario)
    {
        var resultado = mecanicos.Actualizar(id, formulario);
        return new(resultado.Actualizado, resultado.Errores);
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Administrador")]
    public IActionResult Eliminar(int id)
    {
        string? error = mecanicos.Eliminar(id);
        return error is null ? NoContent() : BadRequest(new ErrorApi(error));
    }
}
