using System.ComponentModel.DataAnnotations;
using System.Data.Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using TallerMecanico.Contratos;

namespace TallerMecanico.Controllers;

// Traduce las excepciones de la aplicación a respuestas HTTP con { mensaje }.
public sealed class ErroresApiFilter(ILogger<ErroresApiFilter> logger) : IExceptionFilter
{
    public void OnException(ExceptionContext contexto)
    {
        switch (contexto.Exception)
        {
            case ValidationException error:
                contexto.Result = new BadRequestObjectResult(new ErrorApi(error.Message));
                break;
            case UnauthorizedAccessException error:
                contexto.Result = new ObjectResult(new ErrorApi(error.Message)) { StatusCode = StatusCodes.Status403Forbidden };
                break;
            case DbException error:
                logger.LogError(error, "Falló la base de datos.");
                contexto.Result = new ObjectResult(new ErrorApi("No se pudo acceder a los datos. Intenta nuevamente."))
                {
                    StatusCode = StatusCodes.Status503ServiceUnavailable
                };
                break;
            default:
                return;
        }
        contexto.ExceptionHandled = true;
    }
}
