using MySqlConnector;
using TallerMecanico.Application.Ports;
using TallerMecanico.Models;
using TallerMecanico.Validators;
using TallerMecanico.ViewModels;

namespace TallerMecanico.Services;

public class VehiculoService
{
    private const string MensajePlacaDuplicada =
        "Esta placa ya está registrada.";

    private const string MensajeClienteNoExiste =
        "El cliente seleccionado no existe.";

    private readonly IVehiculoPort _vehiculoPort;

    private readonly IClientePort _clientePort;

    private readonly ValidacionVehiculos _validacionVehiculos;

    public VehiculoService(
        IVehiculoPort vehiculoPort,
        IClientePort clientePort,
        ValidacionVehiculos validacionVehiculos)
    {
        _vehiculoPort =
            vehiculoPort;

        _clientePort =
            clientePort;

        _validacionVehiculos =
            validacionVehiculos;
    }

    public List<Vehiculo> GetAll()
    {
        return _vehiculoPort
            .GetAll()
            .ToList();
    }

    public List<Vehiculo> Search(
        string filtro)
    {
        return _vehiculoPort
            .Search(
                filtro.Trim())
            .ToList();
    }

    public Vehiculo? GetById(
        int id)
    {
        return _vehiculoPort.GetById(
            id);
    }

    public void Delete(
        int id)
    {
        _vehiculoPort.Delete(
            id);
    }

    public bool ExistsPorCliente(
        int clienteId)
    {
        return _vehiculoPort.ExistsPorCliente(
            clienteId);
    }

    public (
        VehiculoFormViewModel Formulario,
        IReadOnlyDictionary<string, string> Errores)
        Create(
            VehiculoFormViewModel formulario)
    {
        return Guardar(
            formulario,
            0);
    }

    public (
        VehiculoFormViewModel Formulario,
        IReadOnlyDictionary<string, string> Errores)
        Update(
            VehiculoFormViewModel formulario)
    {
        return Guardar(
            formulario,
            formulario.Id,
            actualizar: true);
    }

    private (
        VehiculoFormViewModel Formulario,
        IReadOnlyDictionary<string, string> Errores)
        Guardar(
            VehiculoFormViewModel formulario,
            int id,
            bool actualizar = false)
    {
        VehiculoFormViewModel normalizado =
            _validacionVehiculos.Normalizar(
                formulario);

        normalizado.Id =
            id;

        var errores =
            new Dictionary<string, string>(
                _validacionVehiculos.Validar(
                    normalizado));

        if (actualizar &&
            (id <= 0 ||
             _vehiculoPort.GetById(id) is null))
        {
            errores[string.Empty] =
                "El vehículo que intentas editar ya no existe.";
        }

        if (!errores.ContainsKey(
                nameof(
                    VehiculoFormViewModel.Placa))
            &&
            _vehiculoPort.ExistsByPlaca(
                normalizado.Placa ??
                string.Empty,
                id))
        {
            errores[
                nameof(
                    VehiculoFormViewModel.Placa)] =
                MensajePlacaDuplicada;
        }

        ValidarClienteExistente(
            normalizado,
            errores);

        if (errores.Count > 0)
        {
            return (
                normalizado,
                errores);
        }

        Vehiculo vehiculo =
            CrearVehiculo(
                normalizado,
                id);

        try
        {
            if (actualizar)
            {
                _vehiculoPort.Update(
                    vehiculo);
            }
            else
            {
                _vehiculoPort.Add(
                    vehiculo);
            }
        }
        catch (MySqlException exception)
            when (exception.Number == 1062)
        {
            errores[
                nameof(
                    VehiculoFormViewModel.Placa)] =
                MensajePlacaDuplicada;
        }
        catch (MySqlException exception)
            when (exception.Number == 1452)
        {
            errores[
                nameof(
                    VehiculoFormViewModel.ClienteId)] =
                MensajeClienteNoExiste;
        }

        return (
            normalizado,
            errores);
    }

    private void ValidarClienteExistente(
        VehiculoFormViewModel formulario,
        IDictionary<string, string> errores)
    {
        if (!formulario.ClienteId.HasValue)
        {
            return;
        }

        if (errores.ContainsKey(
                nameof(
                    VehiculoFormViewModel.ClienteId)))
        {
            return;
        }

        Cliente? cliente =
            _clientePort.GetById(
                formulario.ClienteId.Value);

        if (cliente is null)
        {
            errores[
                nameof(
                    VehiculoFormViewModel.ClienteId)] =
                MensajeClienteNoExiste;
        }
    }

    private static Vehiculo CrearVehiculo(
        VehiculoFormViewModel formulario,
        int id)
    {
        return new Vehiculo
        {
            Id =
                id,

            Placa =
                formulario.Placa ??
                string.Empty,

            Marca =
                formulario.Marca ??
                string.Empty,

            Modelo =
                formulario.Modelo ??
                string.Empty,

            Kilometraje =
                formulario.Kilometraje ??
                0,

            Observaciones =
                formulario.Observaciones ??
                string.Empty,

            ClienteId =
                formulario.ClienteId
        };
    }
}