using TallerMecanico.Application.Ordenes;

namespace TallerMecanico.Validators;

public sealed class ValidacionOrdenes
{
    public IReadOnlyDictionary<string, string> Validar(CrearOrdenSolicitud solicitud)
    {
        var errores = new Dictionary<string, string>();
        if (solicitud.VehiculoId <= 0) errores[nameof(solicitud.VehiculoId)] = "Selecciona un vehículo.";
        if (solicitud.MecanicoId <= 0) errores[nameof(solicitud.MecanicoId)] = "Selecciona un mecánico.";
        if (solicitud.Fecha.Year < 1000) errores[nameof(solicitud.Fecha)] = "Selecciona una fecha válida.";
        if (solicitud.Token == Guid.Empty) errores[nameof(solicitud.Token)] = "El formulario no tiene un token válido.";
        if (solicitud.Lineas is null || solicitud.Lineas.Count == 0)
            errores[nameof(solicitud.Lineas)] = "Añade al menos un producto.";
        else
        {
            for (int i = 0; i < solicitud.Lineas.Count; i++)
            {
                LineaOrdenSolicitud? linea = solicitud.Lineas[i];
                if (linea is null || linea.ProductoId <= 0) errores[$"Lineas[{i}].ProductoId"] = "Selecciona un producto válido.";
                if (linea is null || linea.Cantidad <= 0) errores[$"Lineas[{i}].Cantidad"] = "La cantidad debe ser mayor que cero.";
            }
        }
        return errores;
    }

    public IReadOnlyList<LineaOrdenSolicitud> AgruparLineas(IReadOnlyList<LineaOrdenSolicitud> lineas)
    {
        var cantidades = new Dictionary<int, int>();
        foreach (LineaOrdenSolicitud linea in lineas)
            cantidades[linea.ProductoId] = checked(cantidades.GetValueOrDefault(linea.ProductoId) + linea.Cantidad);
        return cantidades.OrderBy(item => item.Key).Select(item => new LineaOrdenSolicitud(item.Key, item.Value)).ToArray();
    }
}
