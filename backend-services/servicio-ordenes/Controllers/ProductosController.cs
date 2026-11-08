using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TallerMecanico.Aplicacion.Servicios;
using TallerMecanico.Contratos;
using TallerMecanico.Contratos.Catalogos;

namespace TallerMecanico.Controllers;

[ApiController]
[Route("api/productos")]
[Authorize(Roles = Roles.Personal)]
public sealed class ProductosController(ProductoService productos) : ControllerBase
{
    [HttpGet]
    public IReadOnlyList<ProductoDto> Obtener([FromQuery] string? terminoBusqueda = null) =>
        productos.Buscar(terminoBusqueda ?? string.Empty).Select(producto => producto.ADto()).ToArray();

    [HttpGet("sugerencias")]
    public Task<IReadOnlyList<SugerenciaProducto>> Sugerir([FromQuery] string? termino, CancellationToken cancelacion) =>
        productos.SugerirAsync(termino, cancelacion);

    [HttpGet("{id:int}")]
    public ActionResult<ProductoDto> ObtenerPorId(int id)
    {
        var producto = productos.ObtenerPorId(id);
        return producto is null ? NotFound() : Ok(producto.ADto());
    }

    [HttpPost]
    public ResultadoFormulario<ProductoFormViewModel> Crear([FromBody] ProductoFormViewModel formulario)
    {
        formulario.Id = 0;
        var resultado = productos.Crear(formulario);
        return new(resultado.Formulario, resultado.Errores);
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = Roles.Administrador)]
    public ResultadoFormulario<ProductoFormViewModel> Actualizar(int id, [FromBody] ProductoFormViewModel formulario)
    {
        formulario.Id = id;
        var resultado = productos.Actualizar(formulario);
        return new(resultado.Formulario, resultado.Errores);
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = Roles.Administrador)]
    public IActionResult Eliminar(int id)
    {
        string? error = productos.Eliminar(id);
        return error is null ? NoContent() : BadRequest(new ErrorApi(error));
    }
}
