using MySqlConnector;
using TallerMecanico.Data;
using TallerMecanico.Models;
using TallerMecanico.Patterns.FactoryMethod;
using TallerMecanico.Validators;
using TallerMecanico.ViewModels;

namespace TallerMecanico.Services;

public class VehiculoService
{
    private const string MensajePlacaDuplicada = "Esta placa ya está registrada.";
    private readonly VehiculoRepository _vehiculoRepository;
    private readonly ValidacionVehiculos _validacionVehiculos;

    public VehiculoService(
        CreadorVehiculo creadorVehiculo,
        ValidacionVehiculos validacionVehiculos)
    {
        _vehiculoRepository = (VehiculoRepository)creadorVehiculo.CrearRepositorio();
        _validacionVehiculos = validacionVehiculos;
    }

    public List<Vehiculo> GetAll() => _vehiculoRepository.GetAll();

    public List<Vehiculo> Search(string filtro) => _vehiculoRepository.Search(filtro.Trim());

    public Vehiculo? GetById(int id) => _vehiculoRepository.GetById(id);

    public void Delete(int id) => _vehiculoRepository.Delete(id);

    public (VehiculoFormViewModel Formulario, IReadOnlyDictionary<string, string> Errores)
        Create(VehiculoFormViewModel formulario) => Guardar(formulario, 0);

    public (VehiculoFormViewModel Formulario, IReadOnlyDictionary<string, string> Errores)
        Update(VehiculoFormViewModel formulario) => Guardar(formulario, formulario.Id, actualizar: true);

    private (VehiculoFormViewModel Formulario, IReadOnlyDictionary<string, string> Errores)
        Guardar(VehiculoFormViewModel formulario, int id, bool actualizar = false)
    {
        var normalizado = _validacionVehiculos.Normalizar(formulario);
        normalizado.Id = id;
        var errores = new Dictionary<string, string>(_validacionVehiculos.Validar(normalizado));

        if (actualizar && (id <= 0 || _vehiculoRepository.GetById(id) is null))
        {
            errores[string.Empty] = "El vehículo que intentas editar ya no existe.";
        }

        if (!errores.ContainsKey(nameof(formulario.Placa)) &&
            _vehiculoRepository.ExistsByPlaca(normalizado.Placa ?? string.Empty, id))
        {
            errores[nameof(formulario.Placa)] = MensajePlacaDuplicada;
        }

        if (errores.Count > 0)
        {
            return (normalizado, errores);
        }

        var vehiculo = new Vehiculo
        {
            Id = id,
            Placa = normalizado.Placa ?? string.Empty,
            Marca = normalizado.Marca ?? string.Empty,
            Modelo = normalizado.Modelo ?? string.Empty,
            Kilometraje = normalizado.Kilometraje ?? 0,
            Observaciones = normalizado.Observaciones ?? string.Empty
        };

        try
        {
            if (actualizar)
            {
                _vehiculoRepository.Update(vehiculo);
            }
            else
            {
                _vehiculoRepository.Add(vehiculo);
            }
        }
        catch (MySqlException exception) when (exception.Number == 1062)
        {
            errores[nameof(formulario.Placa)] = MensajePlacaDuplicada;
        }

        return (normalizado, errores);
    }
}
