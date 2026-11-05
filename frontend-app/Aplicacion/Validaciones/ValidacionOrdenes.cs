using TallerMecanico.Aplicacion.CasosUso.Ordenes;

namespace TallerMecanico.Aplicacion.Validaciones;

public sealed class ValidacionOrdenes
{
    public IReadOnlyDictionary<string, string> Validar(CrearOrdenSolicitud solicitud)
    {
        var errores = new Dictionary<string, string>();
        if (solicitud.VehiculoId <= 0) errores[nameof(solicitud.VehiculoId)] = "Selecciona un vehículo.";
        if (solicitud.MecanicoId <= 0) errores[nameof(solicitud.MecanicoId)] = "Selecciona un mecánico.";

        IReadOnlyList<int> servicios = solicitud.ServiciosSeleccionados;
        if (servicios.Count == 0)
            errores["ServicioIds"] = "Agrega al menos un servicio a la orden.";
        else
        {
            if (servicios.Any(id => id <= 0)) errores["ServicioIds"] = "Selecciona servicios válidos.";
            if (servicios.Distinct().Count() != servicios.Count)
                errores["ServicioIds"] = "No agregues el mismo servicio más de una vez.";
        }

        ValidarCantidadesServicios(solicitud.CantidadesServicios, errores);

        if (solicitud.Fecha.Year < 1000) errores[nameof(solicitud.Fecha)] = "Selecciona una fecha válida.";
        if (solicitud.Token == Guid.Empty) errores[nameof(solicitud.Token)] = "El formulario no tiene un token válido.";
        if (solicitud.Lineas is null)
            errores[nameof(solicitud.Lineas)] = "Agrega al menos un producto o un servicio.";
        else if (solicitud.Lineas.Count == 0 && servicios.Count == 0)
            errores[nameof(solicitud.Lineas)] = "Agrega al menos un producto o un servicio.";
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

    private static void ValidarCantidadesServicios(
        IReadOnlyList<LineaServicioOrdenSolicitud>? cantidadesServicios,
        Dictionary<string, string> errores)
    {
        if (cantidadesServicios is null) return;

        var servicios = new HashSet<int>();
        foreach (LineaServicioOrdenSolicitud? linea in cantidadesServicios)
        {
            if (linea is null)
            {
                errores[nameof(CrearOrdenSolicitud.CantidadesServicios)] = "Las cantidades de servicios contienen una entrada inválida.";
                return;
            }
            if (!servicios.Add(linea.ServicioId))
            {
                errores[nameof(CrearOrdenSolicitud.CantidadesServicios)] = "No agregues cantidades duplicadas para el mismo servicio.";
                return;
            }
        }
    }

    public IReadOnlyList<LineaOrdenSolicitud> AgruparLineas(IReadOnlyList<LineaOrdenSolicitud> lineas)
    {
        var cantidades = new Dictionary<int, int>();
        foreach (LineaOrdenSolicitud linea in lineas)
            cantidades[linea.ProductoId] = checked(cantidades.GetValueOrDefault(linea.ProductoId) + linea.Cantidad);
        return cantidades.OrderBy(item => item.Key).Select(item => new LineaOrdenSolicitud(item.Key, item.Value)).ToArray();
    }
}
