using TallerMecanico.Models;

namespace TallerMecanico.Data;

public interface IVehiculoRepository
{
    List<Vehiculo> GetAll();

    Vehiculo? GetById(int id);

    void Add(Vehiculo vehiculo);

    void Update(Vehiculo vehiculo);

    void Delete(int id);

    bool ExistsByPlaca(string placa, int idExcluido);

    int Count();
}
