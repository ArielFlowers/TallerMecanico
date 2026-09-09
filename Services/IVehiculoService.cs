using TallerMecanico.Models;

namespace TallerMecanico.Services;

public interface IVehiculoService
{
    List<Vehiculo> GetAll();

    Vehiculo? GetById(int id);

    void Create(Vehiculo vehiculo);

    void Update(Vehiculo vehiculo);

    void Delete(int id);

    bool PlacaRegistrada(string placa, int idExcluido);
}
