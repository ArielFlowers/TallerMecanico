using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.WebUtilities;
using TallerMecanico.Contratos;

namespace TallerMecanico.Api;

// Base de los clientes HTTP: las respuestas de error vuelven a ser las excepciones
// que las páginas ya sabían manejar.
public abstract class ApiBase(HttpClient http)
{
    protected HttpClient Http => http;

    protected static string ConFiltros(string ruta, params (string Nombre, object? Valor)[] filtros) =>
        QueryHelpers.AddQueryString(ruta, filtros.Select(filtro =>
            new KeyValuePair<string, string?>(filtro.Nombre, filtro.Valor switch
            {
                null => null,
                DateOnly fecha => fecha.ToString("yyyy-MM-dd"),
                _ => filtro.Valor.ToString()
            })));

    protected async Task<T> LeerAsync<T>(string ruta, CancellationToken cancelacion = default)
    {
        using HttpResponseMessage respuesta = await http.GetAsync(ruta, cancelacion);
        await ValidarAsync(respuesta, cancelacion);
        return (await respuesta.Content.ReadFromJsonAsync<T>(cancelacion))!;
    }

    protected async Task<T?> LeerOpcionalAsync<T>(string ruta, CancellationToken cancelacion = default) where T : class
    {
        using HttpResponseMessage respuesta = await http.GetAsync(ruta, cancelacion);
        if (respuesta.StatusCode == HttpStatusCode.NotFound) return null;
        await ValidarAsync(respuesta, cancelacion);
        return await respuesta.Content.ReadFromJsonAsync<T>(cancelacion);
    }

    protected async Task<byte[]> LeerArchivoAsync(string ruta, CancellationToken cancelacion = default)
    {
        using HttpResponseMessage respuesta = await http.GetAsync(ruta, cancelacion);
        await ValidarAsync(respuesta, cancelacion);
        return await respuesta.Content.ReadAsByteArrayAsync(cancelacion);
    }

    protected async Task<byte[]?> LeerArchivoOpcionalAsync(string ruta)
    {
        using HttpResponseMessage respuesta = await http.GetAsync(ruta);
        if (respuesta.StatusCode == HttpStatusCode.NotFound) return null;
        await ValidarAsync(respuesta);
        return await respuesta.Content.ReadAsByteArrayAsync();
    }

    // Para operaciones que siempre responden con su resultado.
    protected async Task<T> GuardarAsync<T>(HttpMethod metodo, string ruta, object? cuerpo = null)
    {
        using var solicitud = new HttpRequestMessage(metodo, ruta);
        if (cuerpo is not null) solicitud.Content = JsonContent.Create(cuerpo, cuerpo.GetType());
        using HttpResponseMessage respuesta = await http.SendAsync(solicitud);
        await ValidarAsync(respuesta);
        return (await respuesta.Content.ReadFromJsonAsync<T>())!;
    }

    // Para operaciones cuyo rechazo la página muestra al usuario.
    protected async Task<ResultadoApi> EnviarAsync(HttpMethod metodo, string ruta, object? cuerpo = null, CancellationToken cancelacion = default)
    {
        using var solicitud = new HttpRequestMessage(metodo, ruta);
        if (cuerpo is not null) solicitud.Content = JsonContent.Create(cuerpo, cuerpo.GetType());
        using HttpResponseMessage respuesta = await http.SendAsync(solicitud, cancelacion);

        if (respuesta.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Conflict or HttpStatusCode.NotFound)
            return new ResultadoApi(false, respuesta.StatusCode, await LeerMensajeAsync(respuesta, cancelacion));

        await ValidarAsync(respuesta, cancelacion);
        return new ResultadoApi(true, respuesta.StatusCode, null);
    }

    protected static async Task ValidarAsync(HttpResponseMessage respuesta, CancellationToken cancelacion = default)
    {
        if (respuesta.IsSuccessStatusCode) return;

        switch (respuesta.StatusCode)
        {
            case HttpStatusCode.BadRequest:
                throw new ValidationException(await LeerMensajeAsync(respuesta, cancelacion));
            case HttpStatusCode.Forbidden:
                throw new UnauthorizedAccessException(await LeerMensajeAsync(respuesta, cancelacion));
            default:
                respuesta.EnsureSuccessStatusCode();
                return;
        }
    }

    protected static async Task<string> LeerMensajeAsync(HttpResponseMessage respuesta, CancellationToken cancelacion = default)
    {
        const string generico = "No se pudo completar la operación. Intenta nuevamente.";
        try
        {
            ErrorApi? error = await respuesta.Content.ReadFromJsonAsync<ErrorApi>(cancelacion);
            return string.IsNullOrWhiteSpace(error?.Mensaje) ? generico : error.Mensaje;
        }
        catch (System.Text.Json.JsonException)
        {
            return generico;
        }
    }
}

public sealed record ResultadoApi(bool Exito, HttpStatusCode Codigo, string? Mensaje);
