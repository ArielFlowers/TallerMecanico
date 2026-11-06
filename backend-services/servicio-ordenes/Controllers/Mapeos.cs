using TallerMecanico.Aplicacion.CasosUso.Ordenes;
using TallerMecanico.Contratos.Catalogos;
using TallerMecanico.Contratos.Ordenes;
using TallerMecanico.Dominio.Entidades;
using EstadoOrdenDto = TallerMecanico.Contratos.Ordenes.EstadoOrden;

namespace TallerMecanico.Controllers;

// Las entidades y resultados internos no salen de la API: se convierten a contratos.
internal static class Mapeos
{
    public static ClienteDto ADto(this Cliente cliente) => new()
    {
        Id = cliente.Id, Ci = cliente.Ci, ComplementoCi = cliente.ComplementoCi, Nombres = cliente.Nombres,
        PrimerApellido = cliente.PrimerApellido, SegundoApellido = cliente.SegundoApellido, Celular = cliente.Celular,
        CreadoPor = cliente.CreadoPor, FechaCreacion = cliente.FechaCreacion
    };

    public static VehiculoDto ADto(this Vehiculo vehiculo) => new()
    {
        Id = vehiculo.Id, Placa = vehiculo.Placa, EsPlacaExtranjera = vehiculo.EsPlacaExtranjera, Marca = vehiculo.Marca,
        Modelo = vehiculo.Modelo, Kilometraje = vehiculo.Kilometraje, Observaciones = vehiculo.Observaciones, ClienteId = vehiculo.ClienteId
    };

    public static ProductoDto ADto(this Producto producto) => new()
    {
        Id = producto.Id, Codigo = producto.Codigo, Nombre = producto.Nombre, Precio = producto.Precio,
        Stock = producto.Stock, StockMinimo = producto.StockMinimo,
        CreadoPor = producto.CreadoPor, FechaCreacion = producto.FechaCreacion
    };

    public static MecanicoDto ADto(this Mecanico mecanico) => new()
    {
        Id = mecanico.Id, Ci = mecanico.Ci, ComplementoCi = mecanico.ComplementoCi, Nombres = mecanico.Nombres,
        PrimerApellido = mecanico.PrimerApellido, SegundoApellido = mecanico.SegundoApellido, Genero = mecanico.Genero,
        Especialidad = mecanico.Especialidad, Celular = mecanico.Celular
    };

    public static ServicioDto ADto(this Servicio servicio) => new()
    {
        Id = servicio.Id, CodigoCatalogo = servicio.CodigoCatalogo, Nombre = servicio.Nombre, Descripcion = servicio.Descripcion,
        Costo = servicio.Costo, TiempoEstimadoHoras = servicio.TiempoEstimadoHoras, UnidadCantidad = servicio.UnidadCantidad
    };

    public static HistorialCostoServicioDto ADto(this HistorialCostoServicio historial) => new()
    {
        Id = historial.Id, ServicioId = historial.ServicioId, NombreServicio = historial.NombreServicio,
        CostoAnterior = historial.CostoAnterior, CostoNuevo = historial.CostoNuevo, FechaCambio = historial.FechaCambio
    };

    public static OrdenResumenDto ADto(this OrdenResumen orden) => new(
        orden.Id, orden.Fecha, orden.Placa, orden.NombreCliente, orden.NombreMecanico, orden.NombreServicio,
        orden.Total, (EstadoOrdenDto)orden.Estado, orden.CreadoPor);

    public static OrdenDetalleDto ADto(this OrdenDetalle orden) => new()
    {
        Id = orden.Id, Fecha = orden.Fecha, Estado = (EstadoOrdenDto)orden.Estado, Total = orden.Total,
        Placa = orden.Placa, Marca = orden.Marca, Modelo = orden.Modelo,
        NombreCliente = orden.NombreCliente, CiCliente = orden.CiCliente,
        ComplementoCiCliente = orden.ComplementoCiCliente, CelularCliente = orden.CelularCliente,
        NombreMecanico = orden.NombreMecanico, CiMecanico = orden.CiMecanico, ComplementoCiMecanico = orden.ComplementoCiMecanico,
        CreadoPor = orden.CreadoPor, FechaCreacion = orden.FechaCreacion,
        AnuladoPor = orden.AnuladoPor, FechaAnulacion = orden.FechaAnulacion,
        Servicio = orden.Servicio?.ADto(),
        Servicios = orden.Servicios.Select(ADto).ToArray(),
        Productos = orden.Productos.Select(producto => new ProductoOrdenDetalleDto(
            producto.Codigo, producto.Nombre, producto.Cantidad, producto.PrecioUnitario, producto.Subtotal)).ToArray()
    };

    public static ResultadoOrdenDto ADto(this ResultadoOrden resultado) =>
        new(resultado.Exito, resultado.OrdenId, resultado.Total, resultado.EsReenvio, resultado.Errores);

    public static CrearOrdenSolicitud ASolicitud(this CrearOrdenDto orden) => new(
        orden.VehiculoId, orden.MecanicoId, orden.Fecha, orden.Token,
        orden.Lineas.Select(linea => new LineaOrdenSolicitud(linea.ProductoId, linea.Cantidad)).ToArray(),
        orden.ServicioId, orden.ServicioIds,
        orden.CantidadesServicios.Select(linea => new LineaServicioOrdenSolicitud(linea.ServicioId, linea.Cantidad)).ToArray());

    private static ServicioOrdenDetalleDto ADto(this ServicioOrdenDetalle servicio) => new()
    {
        Nombre = servicio.Nombre, Descripcion = servicio.Descripcion, Costo = servicio.Costo,
        TiempoEstimadoHoras = servicio.TiempoEstimadoHoras, Cantidad = servicio.Cantidad,
        UnidadCantidad = servicio.UnidadCantidad, PrecioUnitario = servicio.PrecioUnitario
    };
}
