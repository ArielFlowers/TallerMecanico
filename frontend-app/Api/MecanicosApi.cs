namespace TallerMecanico.Api;

// Cliente del Servicio 1: mecánicos.
// Las rutas están en la tabla "Contrato de las APIs" del plan.
public sealed class MecanicosApi(HttpClient http) : ApiBase(http)
{
    public Task<IReadOnlyList<MecanicoDto>> ObtenerAsync(string? terminoBusqueda = null, CancellationToken cancelacion = default) =>
        LeerAsync<IReadOnlyList<MecanicoDto>>(ConFiltros("api/mecanicos", ("terminoBusqueda", terminoBusqueda)), cancelacion);

    public Task<MecanicoDto?> ObtenerPorIdAsync(int id, CancellationToken cancelacion = default) =>
        LeerOpcionalAsync<MecanicoDto>($"api/mecanicos/{id}", cancelacion);

    public Task<IReadOnlyList<SugerenciaMecanico>> SugerirAsync(string? termino, CancellationToken cancelacion = default) =>
        LeerAsync<IReadOnlyList<SugerenciaMecanico>>(ConFiltros("api/mecanicos/sugerencias", ("termino", termino)), cancelacion);

    public Task<IReadOnlyDictionary<string, string>> CrearAsync(MecanicoInputModel formulario) =>
        GuardarAsync<IReadOnlyDictionary<string, string>>(HttpMethod.Post, "api/mecanicos", formulario);

    public Task<ResultadoMecanico> ActualizarAsync(int id, MecanicoInputModel formulario) =>
        GuardarAsync<ResultadoMecanico>(HttpMethod.Put, $"api/mecanicos/{id}", formulario);

    public Task<ResultadoApi> EliminarAsync(int id, CancellationToken cancelacion = default) =>
        EnviarAsync(HttpMethod.Delete, $"api/mecanicos/{id}", cancelacion: cancelacion);
}
