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

    public List<Vehiculo> ObtenerTodos()
    {
        return _vehiculoRepository.GetAll();
    }

    public Vehiculo? ObtenerPorId(int id)
    {
        return _vehiculoRepository.GetById(id);
    }

    public void Crear(Vehiculo vehiculo)
    {
        _vehiculoRepository.Add(vehiculo);
    }

    public void Actualizar(Vehiculo vehiculo)
    {
        _vehiculoRepository.Update(vehiculo);
    }

    public void Eliminar(int id)
    {
        _vehiculoRepository.Delete(id);
    }

    public bool PlacaRegistrada(string placa, int idExcluido)
    {
        return _vehiculoRepository.ExistsByPlaca(placa, idExcluido);
    }
}
