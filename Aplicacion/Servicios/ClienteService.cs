using System.Text.RegularExpressions;
using TallerMecanico.Aplicacion.Errores;
using TallerMecanico.Aplicacion.Puertos;
using TallerMecanico.Dominio.Entidades;
using TallerMecanico.Aplicacion.Validaciones;
using TallerMecanico.Aplicacion.Dto;

namespace TallerMecanico.Aplicacion.Servicios;

public class ClienteService
{
    private const int MinimoCaracteresSugerencias = 2;
    private const int MaximoCaracteresSugerencias = 100;

    private const string MensajeCiDuplicado =
        "Ya existe un cliente registrado con este CI y complemento.";

    private const string MensajeClienteNoExiste =
        "El cliente solicitado no existe.";

    private const string MensajeClienteConVehiculos =
        "No se puede eliminar el cliente porque tiene vehículos asociados.";

    private readonly IClientePort _clientePort;
    private readonly IVehiculoPort _vehiculoPort;
    private readonly ValidacionClientes _validacionClientes;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditoriaPort? _auditoria;


    public ClienteService(
            IClientePort clientePort,
            IVehiculoPort vehiculoPort,
            ValidacionClientes validacionClientes,
            ICurrentUser currentUser,
            IAuditoriaPort? auditoria = null)
    {
        _auditoria = auditoria;
        _clientePort = clientePort;
        _vehiculoPort = vehiculoPort;
        _validacionClientes = validacionClientes;
        _currentUser = currentUser;
    }

    public IReadOnlyList<Cliente> Obtener()
    {
        return _clientePort.GetAll();
    }

    public IReadOnlyList<Cliente> Buscar(
        string terminoBusqueda)
    {
        if (string.IsNullOrWhiteSpace(
                terminoBusqueda))
        {
            return Obtener();
        }

        return _clientePort.Search(
            terminoBusqueda.Trim());
    }

    public Task<IReadOnlyList<SugerenciaCliente>> SugerirAsync(
        string? termino, CancellationToken cancellationToken = default)
    {
        var terminoNormalizado = Regex.Replace((termino ?? string.Empty).Trim(), @"\s+", " ");
        if (terminoNormalizado.Length < MinimoCaracteresSugerencias ||
            terminoNormalizado.Length > MaximoCaracteresSugerencias)
        {
            return Task.FromResult<IReadOnlyList<SugerenciaCliente>>([]);
        }

        return _clientePort.SugerirAsync(terminoNormalizado, cancellationToken);
    }

    public Cliente? ObtenerPorId(
        int id)
    {
        if (id <= 0)
        {
            return null;
        }

        return _clientePort.GetById(id);
    }

    public (
        ClienteFormViewModel Formulario,
        IReadOnlyDictionary<string, string> Errores)
        Crear(
            ClienteFormViewModel formulario)
    {
        ClienteFormViewModel normalizado =
            NormalizarEntrada(
                formulario);

        var errores =
            new Dictionary<string, string>(
                _validacionClientes.Validar(
                    normalizado));

        ValidarCiDuplicado(
            normalizado,
            0,
            errores);

        if (errores.Count > 0)
        {
            return (
                normalizado,
                errores);
        }

        Cliente cliente =
            CrearCliente(
                normalizado);

        try
        {
            _clientePort.Add(
                cliente);

            normalizado.Id = cliente.Id;
        }
        catch (RegistroDuplicadoException)
        {
            errores[
                nameof(
                    ClienteFormViewModel.Ci)] =
                MensajeCiDuplicado;
        }

        return (
            normalizado,
            errores);
    }

    public (
        ClienteFormViewModel Formulario,
        IReadOnlyDictionary<string, string> Errores)
        Actualizar(
            ClienteFormViewModel formulario)
    {
        ClienteFormViewModel normalizado =
            NormalizarEntrada(
                formulario);

        var errores =
            new Dictionary<string, string>(
                _validacionClientes.Validar(
                    normalizado));

        if (normalizado.Id <= 0)
        {
            errores[string.Empty] =
                MensajeClienteNoExiste;

            return (
                normalizado,
                errores);
        }

        Cliente? existente =
            _clientePort.GetById(
                normalizado.Id);

        if (existente is null)
        {
            errores[string.Empty] =
                MensajeClienteNoExiste;

            return (
                normalizado,
                errores);
        }

        ValidarCiDuplicado(
            normalizado,
            normalizado.Id,
            errores);

        if (errores.Count > 0)
        {
            return (
                normalizado,
                errores);
        }

        Cliente cliente =
            new()
            {
                Id =
                    normalizado.Id,

                Ci =
                    normalizado.Ci ??
                    string.Empty,

                ComplementoCi =
                    normalizado.ComplementoCi ??
                    string.Empty,

                Nombres =
                    normalizado.Nombres ??
                    string.Empty,

                PrimerApellido =
                    normalizado.PrimerApellido ??
                    string.Empty,

                SegundoApellido =
                    normalizado.SegundoApellido ??
                    string.Empty,

                Celular =
                    normalizado.Celular ??
                    string.Empty,

                CreadoPor =
                    existente.CreadoPor,

                FechaCreacion =
                    existente.FechaCreacion,

                ModificadoPor =
                    _currentUser.Username
                    ?? throw new UnauthorizedAccessException(
                        "Se requiere un usuario autenticado."),

                FechaModificacion =
                    DateTime.Now
            };

        try
        {
            _clientePort.Update(
                cliente);
        }
        catch (RegistroDuplicadoException)
        {
            errores[
                nameof(
                    ClienteFormViewModel.Ci)] =
                MensajeCiDuplicado;
        }

        return (
            normalizado,
            errores);
    }

    public string? Eliminar(
        int id)
    {
        if (id <= 0)
        {
            return MensajeClienteNoExiste;
        }

        Cliente? cliente =
            _clientePort.GetById(
                id);

        if (cliente is null)
        {
            return MensajeClienteNoExiste;
        }

        // Regla de negocio:
        // un cliente con vehículos relacionados
        // no puede ser eliminado.
        if (_vehiculoPort.ExistsPorCliente(
                id))
        {
            return MensajeClienteConVehiculos;
        }

        try
        {
            _clientePort.Delete(
                id);
        }
        catch (RegistroRelacionadoException)
        {
            // Protección adicional por FK en caso de que
            // aparezca una relación entre la validación
            // y el DELETE.
            return MensajeClienteConVehiculos;
        }

        _auditoria?.Registrar(new RegistroAuditoria
        {
            UsuarioId = _currentUser.UsuarioId,
            Username = _currentUser.Username,
            Accion = "CLIENTE_ELIMINADO",
            Entidad = "Cliente",
            EntidadId = id.ToString(),
            Fecha = DateTime.UtcNow,
            Detalle = $"CI: {cliente.Ci}, Nombre: {cliente.Nombres} {cliente.PrimerApellido}"
        });

        return null;
    }

    private void ValidarCiDuplicado(
        ClienteFormViewModel formulario,
        int idExcluido,
        IDictionary<string, string> errores)
    {
        if (errores.ContainsKey(
                nameof(
                    ClienteFormViewModel.Ci))
            ||
            errores.ContainsKey(
                nameof(
                    ClienteFormViewModel.ComplementoCi)))
        {
            return;
        }

        if (_clientePort.ExistsByCi(
                formulario.Ci ??
                string.Empty,
                formulario.ComplementoCi ??
                string.Empty,
                idExcluido))
        {
            errores[
                nameof(
                    ClienteFormViewModel.Ci)] =
                MensajeCiDuplicado;
        }
    }

    private Cliente CrearCliente(
    ClienteFormViewModel formulario)
    {
        return new Cliente
        {
            Ci =
            formulario.Ci ??
            string.Empty,

            ComplementoCi =
            formulario.ComplementoCi ??
            string.Empty,

            Nombres =
            formulario.Nombres ??
            string.Empty,

            PrimerApellido =
            formulario.PrimerApellido ??
            string.Empty,

            SegundoApellido =
            formulario.SegundoApellido ??
            string.Empty,

            Celular =
            formulario.Celular ??
            string.Empty,

            CreadoPor =
            _currentUser.Username
            ?? throw new UnauthorizedAccessException(
                "Se requiere un usuario autenticado."),

            FechaCreacion =
            DateTime.Now
        };
    }

    private static ClienteFormViewModel NormalizarEntrada(
        ClienteFormViewModel formulario)
    {
        return new ClienteFormViewModel
        {
            Id =
                formulario.Id,

            Ci =
                (formulario.Ci ??
                 string.Empty)
                .Trim(),

            ComplementoCi =
                (formulario.ComplementoCi ??
                 string.Empty)
                .Trim()
                .ToUpperInvariant(),

            Nombres =
                NormalizarNombre(
                    formulario.Nombres),

            PrimerApellido =
                NormalizarNombre(
                    formulario.PrimerApellido),

            SegundoApellido =
                NormalizarNombre(
                    formulario.SegundoApellido),

            Celular =
                (formulario.Celular ??
                 string.Empty)
                .Trim()
        };
    }

    private static string NormalizarNombre(
        string? valor)
    {
        string limpio =
            Regex.Replace(
                (valor ?? string.Empty).Trim(),
                @"\s+",
                " ");

        if (string.IsNullOrWhiteSpace(
                limpio))
        {
            return string.Empty;
        }

        return string.Join(
            " ",
            limpio
                .Split(
                    ' ',
                    StringSplitOptions.RemoveEmptyEntries)
                .Select(
                    FormatearPalabra));
    }

    private static string FormatearPalabra(
        string palabra)
    {
        if (string.IsNullOrEmpty(
                palabra))
        {
            return string.Empty;
        }

        if (palabra.Length == 1)
        {
            return palabra.ToUpperInvariant();
        }

        return
            char.ToUpperInvariant(
                palabra[0])
            +
            palabra[1..]
                .ToLowerInvariant();
    }
}
