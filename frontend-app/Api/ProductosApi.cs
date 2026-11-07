namespace TallerMecanico.Api;

// Cliente del Servicio 1: productos.
// Las rutas están en la tabla "Contrato de las APIs" del plan.
public sealed class ProductosApi(HttpClient http) : ApiBase(http)
{
    public Task<IReadOnlyList<ProductoDto>> ObtenerAsync(string? terminoBusqueda = null, CancellationToken cancelacion = default) =>
        LeerAsync<IReadOnlyList<ProductoDto>>(ConFiltros("api/productos", ("terminoBusqueda", terminoBusqueda)), cancelacion);

    public Task<ProductoDto?> ObtenerPorIdAsync(int id, CancellationToken cancelacion = default) =>
        LeerOpcionalAsync<ProductoDto>($"api/productos/{id}", cancelacion);

    public Task<IReadOnlyList<SugerenciaProducto>> SugerirAsync(string? termino, CancellationToken cancelacion = default) =>
        LeerAsync<IReadOnlyList<SugerenciaProducto>>(ConFiltros("api/productos/sugerencias", ("termino", termino)), cancelacion);

    public Task<ResultadoFormulario<ProductoFormViewModel>> CrearAsync(ProductoFormViewModel formulario) =>
        GuardarAsync<ResultadoFormulario<ProductoFormViewModel>>(HttpMethod.Post, "api/productos", formulario);

    public Task<ResultadoFormulario<ProductoFormViewModel>> ActualizarAsync(ProductoFormViewModel formulario) =>
        GuardarAsync<ResultadoFormulario<ProductoFormViewModel>>(HttpMethod.Put, $"api/productos/{formulario.Id}", formulario);

    public Task<ResultadoApi> EliminarAsync(int id, CancellationToken cancelacion = default) =>
        EnviarAsync(HttpMethod.Delete, $"api/productos/{id}", cancelacion: cancelacion);
}
