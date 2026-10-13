namespace TallerMecanico.Application.Ports;

public interface IUnidadTrabajoOrdenPort
{
    ITransaccionOrden Iniciar();
}

public interface ITransaccionOrden : IDisposable
{
    IOrdenPort Ordenes { get; }
    IDetalleOrdenPort Detalles { get; }
    IProductoStockOrdenPort Productos { get; }
    ICabeceraOrdenPort Cabecera { get; }
    void Confirmar();
    void Revertir();
}
