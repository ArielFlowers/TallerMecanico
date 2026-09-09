using TallerMecanico.Data;
using TallerMecanico.Models;

namespace TallerMecanico.Services;

public class VehiculoService : IVehiculoService
{
    private readonly IVehiculoRepository _vehiculoRepository;

    public VehiculoService(IVehiculoRepository vehiculoRepository)
    {
        _vehiculoRepository = vehiculoRepository;
    }

    public List<Vehiculo> GetAll()
    {
        return _vehiculoRepository.GetAll();
    }

    public Vehiculo? GetById(int id)
    {
        return _vehiculoRepository.GetById(id);
    }

    public void Create(Vehiculo vehiculo)
    {
        _vehiculoRepository.Add(vehiculo);
    }

    public void Update(Vehiculo vehiculo)
    {
        _vehiculoRepository.Update(vehiculo);
    }

    public void Delete(int id)
    {
        _vehiculoRepository.Delete(id);
    }

    public bool PlacaRegistrada(string placa, int idExcluido)
    {
        return _vehiculoRepository.ExistsByPlaca(placa, idExcluido);
    }
}
