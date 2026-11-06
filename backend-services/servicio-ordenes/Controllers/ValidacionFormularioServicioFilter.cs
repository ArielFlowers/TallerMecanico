using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using TallerMecanico.Aplicacion.Validaciones;
using TallerMecanico.Contratos;
using TallerMecanico.Contratos.Catalogos;

namespace TallerMecanico.Controllers;

public sealed class ValidacionFormularioServicioFilter(ValidacionServicios validacion) : IActionFilter
{
    public void OnActionExecuting(ActionExecutingContext contexto)
    {
        var formulario = contexto.ActionArguments.Values.OfType<ServicioFormViewModel>().SingleOrDefault();
        if (formulario is null)
        {
            contexto.Result = new BadRequestObjectResult(new ErrorApi("Los datos del servicio son obligatorios o no tienen un formato válido."));
            return;
        }

        var errores = new Dictionary<string, string>(validacion.Validar(formulario));
        foreach (var campo in contexto.ModelState)
        {
            if (campo.Value?.Errors.FirstOrDefault() is { } error)
                errores[campo.Key] = string.IsNullOrEmpty(error.ErrorMessage) ? "El valor ingresado no es válido." : error.ErrorMessage;
        }

        if (errores.Count > 0)
            contexto.Result = new OkObjectResult(new ResultadoFormulario<ServicioFormViewModel>(formulario, errores));
    }

    public void OnActionExecuted(ActionExecutedContext contexto) { }
}
