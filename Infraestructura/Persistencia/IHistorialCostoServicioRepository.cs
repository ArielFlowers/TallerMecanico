using TallerMecanico.Models;

namespace TallerMecanico.Data;

public interface IHistorialCostoServicioRepository
{
    IReadOnlyList<HistorialCostoServicio> GetAll();
}