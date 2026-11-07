using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Net.Http.Json;

namespace TallerMecanico.Api;

// Cliente del Servicio 1: servicios del taller e historial de costos.
// Las rutas están en la tabla "Contrato de las APIs" del plan.
public sealed class ServiciosApi(HttpClient http) : ApiBase(http)
{
    public Task<IReadOnlyList<ServicioDto>> ObtenerAsync(string? terminoBusqueda = null, CancellationToken cancelacion = default) =>
        LeerAsync<IReadOnlyList<ServicioDto>>(ConFiltros("api/servicios", ("terminoBusqueda", terminoBusqueda)), cancelacion);

    public Task<ServicioDto?> ObtenerPorIdAsync(int id, CancellationToken cancelacion = default) =>
        LeerOpcionalAsync<ServicioDto>($"api/servicios/{id}", cancelacion);

    public Task<IReadOnlyList<SugerenciaServicio>> SugerirAsync(string? termino, CancellationToken cancelacion = default) =>
        LeerAsync<IReadOnlyList<SugerenciaServicio>>(ConFiltros("api/servicios/sugerencias", ("termino", termino)), cancelacion);

    public Task<IReadOnlyList<ServicioDto>> ObtenerCatalogoAsync(CancellationToken cancelacion = default) =>
        LeerAsync<IReadOnlyList<ServicioDto>>("api/servicios/catalogo", cancelacion);

    public Task<IReadOnlyList<SugerenciaServicio>> SugerirCatalogoAsync(string? termino, CancellationToken cancelacion = default) =>
        LeerAsync<IReadOnlyList<SugerenciaServicio>>(ConFiltros("api/servicios/sugerencias",
            ("termino", termino), ("soloCatalogo", true)), cancelacion);

    public Task<IReadOnlyList<HistorialCostoServicioDto>> ObtenerHistorialAsync(CancellationToken cancelacion = default) =>
        LeerAsync<IReadOnlyList<HistorialCostoServicioDto>>("api/servicios/historial", cancelacion);

    public Task<ResultadoFormulario<ServicioFormViewModel>> CrearAsync(ServicioFormViewModel formulario) =>
        GuardarFormularioAsync(HttpMethod.Post, "api/servicios", formulario);

    public Task<ResultadoFormulario<ServicioFormViewModel>> ActualizarAsync(int id, ServicioFormViewModel formulario) =>
        GuardarFormularioAsync(HttpMethod.Put, $"api/servicios/{id}", formulario);

    public Task<ResultadoApi> EliminarAsync(int id, CancellationToken cancelacion = default) =>
        EnviarAsync(HttpMethod.Delete, $"api/servicios/{id}", cancelacion: cancelacion);

    private async Task<ResultadoFormulario<ServicioFormViewModel>> GuardarFormularioAsync(
        HttpMethod metodo, string ruta, ServicioFormViewModel formulario)
    {
        using var solicitud = new HttpRequestMessage(metodo, ruta) { Content = JsonContent.Create(formulario) };
        using var respuesta = await Http.SendAsync(solicitud);
        if (respuesta.StatusCode == HttpStatusCode.Conflict)
            throw new ValidationException(await LeerMensajeAsync(respuesta));
        await ValidarAsync(respuesta);
        return (await respuesta.Content.ReadFromJsonAsync<ResultadoFormulario<ServicioFormViewModel>>())!;
    }
}
