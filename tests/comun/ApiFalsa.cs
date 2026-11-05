using System.Net;
using System.Text;
using System.Text.Json;
using TallerMecanico.Contratos;

namespace TallerMecanico.IntegrationTests;

// Responde por "MÉTODO ruta", sin mirar la consulta. Lo no registrado devuelve 404, como las APIs.
// Uso: var api = new ApiFalsa(); api.Json("GET api/clientes/1", dto);
//      var clientes = new ClientesApi(api.Cliente());
public sealed class ApiFalsa : HttpMessageHandler
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);
    private readonly Dictionary<string, Func<HttpResponseMessage>> _respuestas = [];

    public List<string> Llamadas { get; } = [];

    public List<string> Cuerpos { get; } = [];

    public HttpClient Cliente() => new(this) { BaseAddress = new Uri("http://servicio.prueba/") };

    public void Json<T>(string ruta, T cuerpo) => _respuestas[ruta] = () => new HttpResponseMessage(HttpStatusCode.OK)
    {
        Content = new StringContent(JsonSerializer.Serialize(cuerpo, Web), Encoding.UTF8, "application/json")
    };

    public void Archivo(string ruta, byte[] contenido) => _respuestas[ruta] = () =>
        new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(contenido) };

    public void Error(string ruta, HttpStatusCode codigo, string mensaje = "Detalle interno que no debe mostrarse") =>
        _respuestas[ruta] = () => new HttpResponseMessage(codigo)
        {
            Content = new StringContent(JsonSerializer.Serialize(new ErrorApi(mensaje), Web), Encoding.UTF8, "application/json")
        };

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage solicitud, CancellationToken cancelacion)
    {
        string ruta = solicitud.RequestUri!.AbsolutePath.TrimStart('/');
        Llamadas.Add($"{solicitud.Method} {ruta}{solicitud.RequestUri.Query}");
        if (solicitud.Content is not null) Cuerpos.Add(solicitud.Content.ReadAsStringAsync(cancelacion).Result);
        return Task.FromResult(_respuestas.TryGetValue($"{solicitud.Method} {ruta}", out var respuesta)
            ? respuesta()
            : new HttpResponseMessage(HttpStatusCode.NotFound));
    }
}
