namespace TallerMecanico.Aplicacion.CasosUso.Ordenes;

public sealed record LineaOrdenSolicitud(int ProductoId, int Cantidad);
public sealed record LineaServicioOrdenSolicitud(int ServicioId, int Cantidad);

// Los precios y el total no son parte de la solicitud del navegador.
public sealed record CrearOrdenSolicitud(
    int VehiculoId,
    int MecanicoId,
    DateTime Fecha,
    Guid Token,
    IReadOnlyList<LineaOrdenSolicitud> Lineas,
    int? ServicioId = null,
    IReadOnlyList<int>? ServicioIds = null,
    IReadOnlyList<LineaServicioOrdenSolicitud>? CantidadesServicios = null)
{
    public IReadOnlyList<int> ServiciosSeleccionados => ServicioIds is { Count: > 0 }
        ? ServicioIds
        : ServicioId.HasValue ? [ServicioId.Value] : [];
}
