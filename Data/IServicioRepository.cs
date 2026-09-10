using TallerMecanico.Models;

namespace TallerMecanico.Data;

public interface IServicioRepository
{
    List<Servicio> GetAll();

    Servicio? GetById(int id);

    void Add(Servicio servicio);

    void Update(Servicio servicio);

    void Delete(int id);

    int Count();
}
