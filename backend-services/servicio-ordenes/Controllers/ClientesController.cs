using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TallerMecanico.Aplicacion.Servicios;
using TallerMecanico.Contratos;
using TallerMecanico.Contratos.Catalogos;

namespace TallerMecanico.Controllers;

[ApiController]
[Route("api/clientes")]
[Authorize(Roles = Roles.Personal)]
public sealed class ClientesController(ClienteService clientes) : ControllerBase
{
    [HttpGet]
    public IReadOnlyList<ClienteDto> Obtener([FromQuery] string? terminoBusqueda = null) =>
        clientes.Buscar(terminoBusqueda ?? string.Empty).Select(cliente => cliente.ADto()).ToArray();

    [HttpGet("sugerencias")]
    public Task<IReadOnlyList<SugerenciaCliente>> Sugerir([FromQuery] string? termino, CancellationToken cancelacion) =>
        clientes.SugerirAsync(termino, cancelacion);

    [HttpGet("{id:int}")]
    public ActionResult<ClienteDto> ObtenerPorId(int id)
    {
        var cliente = clientes.ObtenerPorId(id);
        return cliente is null ? NotFound() : Ok(cliente.ADto());
    }

    [HttpPost]
    public ResultadoFormulario<ClienteFormViewModel> Crear([FromBody] ClienteFormViewModel formulario)
    {
        formulario.Id = 0;
        var resultado = clientes.Crear(formulario);
        return new(resultado.Formulario, resultado.Errores);
    }

    [HttpPut("{id:int}")]
    public ResultadoFormulario<ClienteFormViewModel> Actualizar(int id, [FromBody] ClienteFormViewModel formulario)
    {
        formulario.Id = id;
        var resultado = clientes.Actualizar(formulario);
        return new(resultado.Formulario, resultado.Errores);
    }

    [HttpDelete("{id:int}")]
    public IActionResult Eliminar(int id)
    {
        string? error = clientes.Eliminar(id);
        return error is null ? NoContent() : BadRequest(new ErrorApi(error));
    }
}
