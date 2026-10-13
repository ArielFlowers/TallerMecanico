using TallerMecanico.Dominio.Entidades;

namespace TallerMecanico.Infraestructura.Persistencia;

public interface IHistorialCostoServicioRepository
{
    IReadOnlyList<HistorialCostoServicio> GetAll();
}