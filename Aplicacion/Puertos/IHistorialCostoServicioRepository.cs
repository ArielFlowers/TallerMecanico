using TallerMecanico.Dominio.Entidades;

namespace TallerMecanico.Aplicacion.Puertos;

public interface IHistorialCostoServicioRepository
{
    IReadOnlyList<HistorialCostoServicio> GetAll();
}