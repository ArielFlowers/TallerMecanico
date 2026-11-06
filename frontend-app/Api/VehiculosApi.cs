namespace TallerMecanico.Api;

// Cliente del Servicio 1: vehículos.
// Las rutas están en la tabla "Contrato de las APIs" del plan.
public sealed class VehiculosApi(HttpClient http) : ApiBase(http)
{
    public Task<IReadOnlyList<VehiculoDto>> ObtenerAsync(string? terminoBusqueda = null, CancellationToken cancelacion = default) =>
        LeerAsync<IReadOnlyList<VehiculoDto>>(ConFiltros("api/vehiculos", ("terminoBusqueda", terminoBusqueda)), cancelacion);

    public Task<VehiculoDto?> ObtenerPorIdAsync(int id, CancellationToken cancelacion = default) =>
        LeerOpcionalAsync<VehiculoDto>($"api/vehiculos/{id}", cancelacion);

    public Task<IReadOnlyList<SugerenciaVehiculo>> SugerirAsync(string? termino, CancellationToken cancelacion = default) =>
        LeerAsync<IReadOnlyList<SugerenciaVehiculo>>(ConFiltros("api/vehiculos/sugerencias", ("termino", termino)), cancelacion);

    public Task<IReadOnlyDictionary<string, IReadOnlyList<string>>> ObtenerCatalogoAsync(CancellationToken cancelacion = default) =>
        LeerAsync<IReadOnlyDictionary<string, IReadOnlyList<string>>>("api/vehiculos/catalogo", cancelacion);

    public Task<ResultadoFormulario<VehiculoFormViewModel>> CrearAsync(VehiculoFormViewModel formulario) =>
        GuardarAsync<ResultadoFormulario<VehiculoFormViewModel>>(HttpMethod.Post, "api/vehiculos", formulario);

    public Task<ResultadoFormulario<VehiculoFormViewModel>> ActualizarAsync(VehiculoFormViewModel formulario) =>
        GuardarAsync<ResultadoFormulario<VehiculoFormViewModel>>(HttpMethod.Put, $"api/vehiculos/{formulario.Id}", formulario);

    public Task<ResultadoApi> EliminarAsync(int id, CancellationToken cancelacion = default) =>
        EnviarAsync(HttpMethod.Delete, $"api/vehiculos/{id}", cancelacion: cancelacion);
}
