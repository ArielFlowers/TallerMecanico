using TallerMecanico.Dominio.Entidades;

namespace TallerMecanico.Aplicacion.CasosUso.Ordenes.Contratos;

public interface IOrdenRepository
{
    Orden? ObtenerPorToken(Guid token);
    Orden? ObtenerParaAnular(int id);
    void Insertar(Orden orden);
    void InsertarServicio(DetalleServicioOrden servicio);
    void ActualizarTotal(int ordenId, decimal total);
    bool MarcarAnulada(int ordenId, string usuario, DateTime fechaUtc);
}
