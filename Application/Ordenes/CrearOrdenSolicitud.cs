namespace TallerMecanico.Application.Ordenes;

public sealed record LineaOrdenSolicitud(int ProductoId, int Cantidad);

// Los precios y el total no son parte de la solicitud del navegador.
public sealed record CrearOrdenSolicitud(
    int VehiculoId,
    int MecanicoId,
    DateTime Fecha,
    Guid Token,
    IReadOnlyList<LineaOrdenSolicitud> Lineas);
