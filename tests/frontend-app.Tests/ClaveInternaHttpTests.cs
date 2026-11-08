using System.Net;

namespace TallerMecanico.IntegrationTests;

// Las APIs reales: solo atienden al Frontend y aplican los roles que este informa.
[Trait("Area", "API")]
public sealed class ClaveInternaHttpTests
{
    [MySqlAisladoFact]
    public async Task Apis_RechazanLlamadasSinLaClaveYAplicanElRol()
    {
        using var aislada = new MySqlAisladoFixture();
        using var servicios = ServiciosEnPrueba.Iniciar(aislada.CadenaConexion);
        using var cliente = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };

        foreach (var (url, ruta) in new[]
        {
            (servicios.Url("Servicios__OrdenesUrl"), "api/ordenes"),
            (servicios.Url("Servicios__UsuariosUrl"), "api/usuarios/resumen"),
            (servicios.Url("Servicios__ReportesUrl"), "api/reportes/ordenes")
        })
        {
            Assert.Equal(HttpStatusCode.OK, (await cliente.GetAsync($"{url}/salud")).StatusCode);

            Assert.Equal(HttpStatusCode.Unauthorized, await Pedir(cliente, url, ruta));
            Assert.Equal(HttpStatusCode.Unauthorized, await Pedir(cliente, url, ruta, "clave-incorrecta", "Administrador"));
            Assert.Equal(HttpStatusCode.Forbidden, await Pedir(cliente, url, ruta, ServiciosEnPrueba.ClaveInterna));
            Assert.Equal(HttpStatusCode.OK, await Pedir(cliente, url, ruta, ServiciosEnPrueba.ClaveInterna, "Administrador"));
        }

        string reportes = servicios.Url("Servicios__ReportesUrl");
        Assert.Equal(HttpStatusCode.Forbidden,
            await Pedir(cliente, reportes, "api/reportes/ordenes", ServiciosEnPrueba.ClaveInterna, "Recepcionista"));
    }

    private static async Task<HttpStatusCode> Pedir(HttpClient cliente, string url, string ruta, string? clave = null, string? rol = null)
    {
        using var solicitud = new HttpRequestMessage(HttpMethod.Get, $"{url}/{ruta}");
        if (clave is not null) solicitud.Headers.Add("X-Clave-Interna", clave);
        if (rol is not null)
        {
            solicitud.Headers.Add("X-Usuario-Nombre", "prueba");
            solicitud.Headers.Add("X-Usuario-Rol", rol);
        }
        using HttpResponseMessage respuesta = await cliente.SendAsync(solicitud);
        return respuesta.StatusCode;
    }
}
