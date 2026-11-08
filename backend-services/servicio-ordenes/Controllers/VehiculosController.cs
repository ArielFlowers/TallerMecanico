using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TallerMecanico.Aplicacion.Servicios;
using TallerMecanico.Contratos;
using TallerMecanico.Contratos.Catalogos;
using TallerMecanico.Dominio.Entidades;

namespace TallerMecanico.Controllers;

[ApiController]
[Route("api/vehiculos")]
[Authorize(Roles = Roles.Personal)]
public sealed class VehiculosController(VehiculoService vehiculos) : ControllerBase
{
    [HttpGet]
    public IReadOnlyList<VehiculoDto> Obtener([FromQuery] string? terminoBusqueda = null) =>
        (string.IsNullOrWhiteSpace(terminoBusqueda) ? vehiculos.GetAll() : vehiculos.Search(terminoBusqueda))
            .Select(vehiculo => vehiculo.ADto()).ToArray();

    [HttpGet("sugerencias")]
    public Task<IReadOnlyList<SugerenciaVehiculo>> Sugerir([FromQuery] string? termino, CancellationToken cancelacion) =>
        vehiculos.SugerirAsync(termino, cancelacion);

    [HttpGet("catalogo")]
    public IReadOnlyDictionary<string, IReadOnlyList<string>> ObtenerCatalogo() => CatalogoVehiculos.CatalogoOrdenado;

    [HttpGet("{id:int}")]
    public ActionResult<VehiculoDto> ObtenerPorId(int id)
    {
        var vehiculo = vehiculos.GetById(id);
        return vehiculo is null ? NotFound() : Ok(vehiculo.ADto());
    }

    [HttpPost]
    public ResultadoFormulario<VehiculoFormViewModel> Crear([FromBody] VehiculoFormViewModel formulario)
    {
        formulario.Id = 0;
        var resultado = vehiculos.Create(formulario);
        return new(resultado.Formulario, resultado.Errores);
    }

    [HttpPut("{id:int}")]
    public ResultadoFormulario<VehiculoFormViewModel> Actualizar(int id, [FromBody] VehiculoFormViewModel formulario)
    {
        formulario.Id = id;
        var resultado = vehiculos.Update(formulario);
        return new(resultado.Formulario, resultado.Errores);
    }

    [HttpDelete("{id:int}")]
    public IActionResult Eliminar(int id)
    {
        string? error = vehiculos.Delete(id);
        return error is null ? NoContent() : BadRequest(new ErrorApi(error));
    }
}
