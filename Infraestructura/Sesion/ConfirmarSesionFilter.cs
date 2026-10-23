using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace TallerMecanico.Infraestructura.Sesion;

public sealed class ConfirmarSesionFilter(ILogger<ConfirmarSesionFilter> logger) : IAsyncPageFilter
{
    public Task OnPageHandlerSelectionAsync(PageHandlerSelectedContext context) => Task.CompletedTask;

    public async Task OnPageHandlerExecutionAsync(PageHandlerExecutingContext context, PageHandlerExecutionDelegate next)
    {
        PageHandlerExecutedContext resultado = await next();
        if (resultado.Exception is SesionNoDisponibleException fallo)
        {
            InformarFallo(resultado, "Carga", fallo.InnerException!);
            return;
        }
        if (resultado.Exception is not null || !context.HttpContext.Items.ContainsKey(SesionDisponible.ClaveUso)) return;

        try
        {
            // Antes de ejecutar la vista/redirección: no confirmar borradores
            // que el commit implícito del middleware pudiera perder silenciosamente.
            await context.HttpContext.Session.CommitAsync(context.HttpContext.RequestAborted);
        }
        catch (Exception error) when (SesionDisponible.EsFalloAlmacenamiento(error, context.HttpContext))
        {
            InformarFallo(resultado, "Confirmacion", error);
        }
    }

    private void InformarFallo(PageHandlerExecutedContext contexto, string operacion, Exception error)
    {
        logger.LogWarning("SESION NO DISPONIBLE | operación {Operacion} | réplica {Replica} | tipo {TipoError} | sin confirmación del estado distribuido.",
            operacion, Environment.MachineName, error.GetType().Name);
        contexto.ExceptionHandled = true;
        contexto.HttpContext.Response.Headers.CacheControl = "no-store";
        contexto.Result = new ContentResult
        {
            StatusCode = StatusCodes.Status503ServiceUnavailable,
            ContentType = "text/plain; charset=utf-8",
            Content = "No se pudo confirmar el estado de la sesión. Intenta nuevamente cuando se restablezca el servicio. Si estabas guardando, comprueba el resultado antes de reenviar el formulario."
        };
    }
}
