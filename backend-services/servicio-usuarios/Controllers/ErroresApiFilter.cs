using System.ComponentModel.DataAnnotations;
using System.Data.Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using TallerMecanico.Contratos;
using TallerMecanico.Aplicacion.Errores;

namespace TallerMecanico.Controllers;

// Traduce las excepciones de la aplicación a respuestas HTTP con { mensaje }.
public sealed class ErroresApiFilter(ILogger<ErroresApiFilter> logger) : IExceptionFilter
{
    public void OnException(ExceptionContext contexto)
    {
        switch (contexto.Exception)
        {
            case SolicitudRegistroDuplicadaException error:
                contexto.Result = new ConflictObjectResult(new ErrorApi(
                    "Ya existe un usuario o solicitud pendiente con ese valor.", error.Campo, "SolicitudDuplicada"));
                break;
            case SolicitudGuardadaCorreoPendienteException:
                contexto.Result = new ObjectResult(new ErrorApi(
                    "La invitación se guardó pero no se pudo enviar el correo. Intente reenviar la verificación más tarde.",
                    Codigo: "CorreoPendiente")) { StatusCode = StatusCodes.Status503ServiceUnavailable };
                break;
            case ConfiguracionRegistroException:
                contexto.Result = new ObjectResult(new ErrorApi(
                    "Error de configuración del servidor de correo. Contacte al administrador del sistema.",
                    Codigo: "ConfiguracionCorreo")) { StatusCode = StatusCodes.Status503ServiceUnavailable };
                break;
            case EnvioVerificacionException:
                contexto.Result = new ObjectResult(new ErrorApi(
                    "No se pudo enviar el correo de verificación. Intente nuevamente más tarde.",
                    Codigo: "EnvioCorreo")) { StatusCode = StatusCodes.Status503ServiceUnavailable };
                break;
            case ArgumentException error:
                contexto.Result = new BadRequestObjectResult(new ErrorApi(error.Message));
                break;
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
