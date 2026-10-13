using TallerMecanico.Models;

namespace TallerMecanico.Application.Ports;

public interface IOrdenPort
{
    Orden? ObtenerPorToken(Guid token);
    Orden? ObtenerParaAnular(int id);
    void Insertar(Orden orden);
    void ActualizarTotal(int ordenId, decimal total);
    bool MarcarAnulada(int ordenId, string usuario, DateTime fechaUtc);
}
