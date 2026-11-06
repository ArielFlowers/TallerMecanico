namespace TallerMecanico.Api;

// Cliente del Servicio 1: clientes.
// Las rutas están en la tabla "Contrato de las APIs" del plan.
public sealed class ClientesApi(HttpClient http) : ApiBase(http)
{
    public Task<IReadOnlyList<ClienteDto>> ObtenerAsync(string? terminoBusqueda = null, CancellationToken cancelacion = default) =>
        LeerAsync<IReadOnlyList<ClienteDto>>(ConFiltros("api/clientes", ("terminoBusqueda", terminoBusqueda)), cancelacion);

    public Task<ClienteDto?> ObtenerPorIdAsync(int id, CancellationToken cancelacion = default) =>
        LeerOpcionalAsync<ClienteDto>($"api/clientes/{id}", cancelacion);

    public Task<IReadOnlyList<SugerenciaCliente>> SugerirAsync(string? termino, CancellationToken cancelacion = default) =>
        LeerAsync<IReadOnlyList<SugerenciaCliente>>(ConFiltros("api/clientes/sugerencias", ("termino", termino)), cancelacion);

    public Task<ResultadoFormulario<ClienteFormViewModel>> CrearAsync(ClienteFormViewModel formulario) =>
        GuardarAsync<ResultadoFormulario<ClienteFormViewModel>>(HttpMethod.Post, "api/clientes", formulario);

    public Task<ResultadoFormulario<ClienteFormViewModel>> ActualizarAsync(ClienteFormViewModel formulario) =>
        GuardarAsync<ResultadoFormulario<ClienteFormViewModel>>(HttpMethod.Put, $"api/clientes/{formulario.Id}", formulario);

    public Task<ResultadoApi> EliminarAsync(int id, CancellationToken cancelacion = default) =>
        EnviarAsync(HttpMethod.Delete, $"api/clientes/{id}", cancelacion: cancelacion);
}
