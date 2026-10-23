using MySqlConnector;
using System.Text.RegularExpressions;
using TallerMecanico.Aplicacion.Puertos;
using TallerMecanico.Dominio.Entidades;
using TallerMecanico.Aplicacion.Validaciones;
using TallerMecanico.Aplicacion.Dto;

namespace TallerMecanico.Aplicacion.Servicios;

public class VehiculoService
{
    private const int MinimoCaracteresSugerencias = 2;
    private const int MaximoCaracteresSugerencias = 100;

    private const string MensajePlacaDuplicada =
        "Esta placa ya está registrada.";

    private const string MensajeClienteNoExiste =
        "El cliente seleccionado no existe.";

    private readonly IVehiculoPort _vehiculoPort;

    private readonly IClientePort _clientePort;

    private readonly ValidacionVehiculos _validacionVehiculos;

    private readonly ICurrentUser? _currentUser;

    private readonly ICacheResumenDashboardPort? _cache;

    public VehiculoService(
        IVehiculoPort vehiculoPort,
        IClientePort clientePort,
        ValidacionVehiculos validacionVehiculos,
        ICurrentUser? currentUser = null,
        ICacheResumenDashboardPort? cache = null)
    {
        _vehiculoPort =
            vehiculoPort;

        _clientePort =
            clientePort;

        _validacionVehiculos =
            validacionVehiculos;

        _currentUser =
            currentUser;

        _cache =
            cache;
    }

    public List<Vehiculo> GetAll()
    {
        return _vehiculoPort
            .GetAll()
            .OrderBy(vehiculo => vehiculo.Placa, CatalogoVehiculos.ComparadorAlfabetico)
            .ThenBy(vehiculo => vehiculo.Id)
            .ToList();
    }

    public List<Vehiculo> Search(
        string filtro)
    {
        return _vehiculoPort
            .Search(
                filtro.Trim())
            .OrderBy(vehiculo => vehiculo.Placa, CatalogoVehiculos.ComparadorAlfabetico)
            .ThenBy(vehiculo => vehiculo.Id)
            .ToList();
    }

    public Task<IReadOnlyList<SugerenciaVehiculo>> SugerirAsync(
        string? termino, CancellationToken cancellationToken = default)
    {
        var terminoNormalizado = Regex.Replace((termino ?? string.Empty).Trim(), @"\s+", " ");
        if (terminoNormalizado.Length < MinimoCaracteresSugerencias ||
            terminoNormalizado.Length > MaximoCaracteresSugerencias)
        {
            return Task.FromResult<IReadOnlyList<SugerenciaVehiculo>>([]);
        }

        return _vehiculoPort.SugerirAsync(terminoNormalizado, cancellationToken);
    }

    public Vehiculo? GetById(
        int id)
    {
        return _vehiculoPort.GetById(
            id);
    }

    public string? Delete(
        int id)
    {
        if (id <= 0 || _vehiculoPort.GetById(id) is null)
            return "El vehículo seleccionado ya no existe.";
        try { _vehiculoPort.Delete(id); }
        catch (MySqlException error) when (error.Number == 1451)
        { return "No se puede eliminar el vehículo porque tiene órdenes asociadas."; }
        _cache?.InvalidarResumen();
        return null;
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

        Vehiculo? existente =
            actualizar
                ? _vehiculoPort.GetById(id)
                : null;

        Vehiculo vehiculo =
            CrearVehiculo(
                normalizado,
                id,
                existente,
                UsuarioActual());

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

                _cache?.InvalidarResumen();
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
        int id,
        Vehiculo? existente,
        string usuario)
    {
        return new Vehiculo
        {
            Id =
                id,

            Placa =
                formulario.Placa ??
                string.Empty,

            EsPlacaExtranjera = formulario.EsPlacaExtranjera,

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
                formulario.ClienteId,

            CreadoPor =
                existente?.CreadoPor
                ?? usuario,

            FechaCreacion =
                existente?.FechaCreacion
                ?? DateTime.Now,

            ModificadoPor =
                existente is null
                    ? null
                    : usuario,

            FechaModificacion =
                existente is null
                    ? null
                    : DateTime.Now
        };
    }

    private string UsuarioActual()
    {
        return string.IsNullOrWhiteSpace(
                _currentUser?.Username)
            ? "sistema"
            : _currentUser!.Username!;
    }
}
