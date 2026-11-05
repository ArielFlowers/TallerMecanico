namespace TallerMecanico.Aplicacion.CasosUso.Ordenes.Contratos;

public interface IUnidadTrabajoOrden
{
    ITransaccionOrden Iniciar();
}

public interface ITransaccionOrden : IDisposable
{
    IOrdenRepository Ordenes { get; }
    IDetalleOrdenRepository Detalles { get; }
    IProductoStockRepository Productos { get; }
    ICabeceraOrdenRepository Cabecera { get; }
    void Confirmar();
    void Revertir();
}
