using System.Text.RegularExpressions;
using MySqlConnector;
using TallerMecanico.Application.Ports;
using TallerMecanico.Models;
using TallerMecanico.Validators;
using TallerMecanico.ViewModels;

namespace TallerMecanico.Services;

public class ClienteService
{
    private const string MensajeCiDuplicado =
        "Ya existe un cliente registrado con este CI y complemento.";

    private const string UsuarioTemporal =
        "sistema";

    private static readonly Regex EspaciosConsecutivos =
        new(@"\s+", RegexOptions.Compiled);

    private readonly IClientePort _clientePort;
    private readonly ValidacionClientes _validacionClientes;

    public ClienteService(
        IClientePort clientePort,
        ValidacionClientes validacionClientes)
    {
        _clientePort = clientePort;
        _validacionClientes = validacionClientes;
    }

    public IReadOnlyList<Cliente> Obtener(
        string? terminoBusqueda = null)
    {
        if (string.IsNullOrWhiteSpace(terminoBusqueda))
        {
            return _clientePort.GetAll();
        }

        return _clientePort.Search(
            terminoBusqueda.Trim());
    }

    public Cliente? ObtenerPorId(int id)
    {
        return _clientePort.GetById(id);
    }

    public IReadOnlyDictionary<string, string> Crear(
        ClienteFormViewModel clienteInput)
    {
        ClienteFormViewModel clienteNormalizado =
            NormalizarEntrada(clienteInput);

        Dictionary<string, string> errores =
            ObtenerErrores(clienteNormalizado);

        if (errores.Count > 0)
        {
            return errores;
        }

        Cliente cliente =
            CrearCliente(clienteNormalizado);

        try
        {
            _clientePort.Add(cliente);
        }
        catch (MySqlException exception)
            when (exception.Number == 1062)
        {
            errores[nameof(ClienteFormViewModel.Ci)] =
                MensajeCiDuplicado;
        }

        return errores;
    }

    public (
        bool Actualizado,
        IReadOnlyDictionary<string, string> Errores)
        Actualizar(
            int id,
            ClienteFormViewModel clienteInput)
    {
        Cliente? clienteExistente =
            _clientePort.GetById(id);

        if (id <= 0 || clienteExistente is null)
        {
            return (
                false,
                new Dictionary<string, string>());
        }

        ClienteFormViewModel clienteNormalizado =
            NormalizarEntrada(clienteInput);

        Dictionary<string, string> errores =
            ObtenerErrores(
                clienteNormalizado,
                id);

        if (errores.Count > 0)
        {
            return (false, errores);
        }

        Cliente cliente =
            CrearCliente(clienteNormalizado);

        cliente.Id = id;

        // Se conservan los datos originales de auditoría.
        cliente.CreadoPor =
            clienteExistente.CreadoPor;

        cliente.FechaCreacion =
            clienteExistente.FechaCreacion;

        try
        {
            _clientePort.Update(cliente);
        }
        catch (MySqlException exception)
            when (exception.Number == 1062)
        {
            errores[nameof(ClienteFormViewModel.Ci)] =
                MensajeCiDuplicado;

            return (false, errores);
        }

        return (true, errores);
    }

    public bool Eliminar(int id)
    {
        if (id <= 0 ||
            _clientePort.GetById(id) is null)
        {
            return false;
        }

        _clientePort.Delete(id);

        return true;
    }

    private Dictionary<string, string> ObtenerErrores(
        ClienteFormViewModel cliente,
        int idExcluido = 0)
    {
        var errores =
            new Dictionary<string, string>(
                _validacionClientes.Validar(cliente));

        if (errores.Count > 0)
        {
            return errores;
        }

        if (_clientePort.ExistsByCi(
                cliente.Ci ?? string.Empty,
                cliente.ComplementoCi ?? string.Empty,
                idExcluido))
        {
            errores[nameof(ClienteFormViewModel.Ci)] =
                MensajeCiDuplicado;
        }

        return errores;
    }

    private static ClienteFormViewModel NormalizarEntrada(
        ClienteFormViewModel clienteInput)
    {
        return new ClienteFormViewModel
        {
            Id = clienteInput.Id,

            Ci = NormalizarCi(
                clienteInput.Ci),

            ComplementoCi =
                NormalizarComplementoCi(
                    clienteInput.ComplementoCi),

            Nombres =
                NormalizarNombre(
                    clienteInput.Nombres),

            PrimerApellido =
                NormalizarNombre(
                    clienteInput.PrimerApellido),

            SegundoApellido =
                NormalizarNombre(
                    clienteInput.SegundoApellido),

            Celular =
                (clienteInput.Celular ?? string.Empty)
                    .Trim()
        };
    }

    private static string NormalizarCi(
        string? ci)
    {
        return (ci ?? string.Empty)
            .Trim();
    }

    private static string NormalizarComplementoCi(
        string? complementoCi)
    {
        return (complementoCi ?? string.Empty)
            .Trim()
            .ToUpperInvariant();
    }

    private static string NormalizarNombre(
        string? nombre)
    {
        string nombreSinEspaciosInnecesarios =
            NormalizarEspacios(nombre);

        if (string.IsNullOrEmpty(
                nombreSinEspaciosInnecesarios))
        {
            return string.Empty;
        }

        string[] palabras =
            nombreSinEspaciosInnecesarios.Split(
                ' ',
                StringSplitOptions.RemoveEmptyEntries);

        return string.Join(
            " ",
            palabras.Select(FormatearPalabra));
    }

    private static string NormalizarEspacios(
        string? texto)
    {
        string textoSinEspaciosExternos =
            (texto ?? string.Empty).Trim();

        return EspaciosConsecutivos.Replace(
            textoSinEspaciosExternos,
            " ");
    }

    private static string FormatearPalabra(
        string palabra)
    {
        if (palabra.Length == 1)
        {
            return palabra.ToUpperInvariant();
        }

        return
            $"{char.ToUpperInvariant(palabra[0])}" +
            palabra[1..].ToLowerInvariant();
    }

    private static Cliente CrearCliente(
        ClienteFormViewModel clienteInput)
    {
        return new Cliente
        {
            Ci =
                clienteInput.Ci ??
                string.Empty,

            ComplementoCi =
                clienteInput.ComplementoCi ??
                string.Empty,

            Nombres =
                clienteInput.Nombres ??
                string.Empty,

            PrimerApellido =
                clienteInput.PrimerApellido ??
                string.Empty,

            SegundoApellido =
                clienteInput.SegundoApellido ??
                string.Empty,

            Celular =
                clienteInput.Celular ??
                string.Empty,

            // Temporal hasta integrar Login/Sesión de Tarjeta 1.
            CreadoPor =
                UsuarioTemporal,

            FechaCreacion =
                DateTime.Now
        };
    }
}