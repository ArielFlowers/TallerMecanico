using System.Text.RegularExpressions;
using TallerMecanico.Aplicacion.Puertos;
using TallerMecanico.Dominio.Entidades;
using TallerMecanico.Infraestructura.Persistencia;
using TallerMecanico.Infraestructura.Persistencia.Fabricas;
using TallerMecanico.Aplicacion.Validaciones;
using TallerMecanico.Aplicacion.Dto;

namespace TallerMecanico.Aplicacion.Servicios;

public class MecanicoService
{
    private const int MinimoCaracteresSugerencias = 2;
    private const int MaximoCaracteresSugerencias = 100;

    private const string MensajeCiDuplicado =
        "Ya existe un mecánico registrado con este CI.";

    private static readonly Regex EspaciosConsecutivos = new(@"\s+", RegexOptions.Compiled);

    private readonly MecanicoRepository _mecanicoRepository;
    private readonly IConsultaMecanicosPort _consultaMecanicos;
    private readonly ValidacionMecanicos _validacionMecanicos;
    private readonly ICurrentUser? _currentUser;

    public MecanicoService(
        CreadorMecanico creadorMecanico,
        ValidacionMecanicos validacionMecanicos,
        ICurrentUser? currentUser = null)
    {
        _mecanicoRepository = (MecanicoRepository)creadorMecanico.CrearRepositorio();
        _consultaMecanicos = _mecanicoRepository;
        _validacionMecanicos = validacionMecanicos;
        _currentUser = currentUser;
    }

    public IReadOnlyDictionary<string, string> Crear(
        MecanicoInputModel mecanicoInput)
    {
        var mecanicoNormalizado = NormalizarEntrada(mecanicoInput);
        var errores = ObtenerErrores(mecanicoNormalizado);

        if (errores.Count > 0)
        {
            return errores;
        }

        _mecanicoRepository.Add(CrearMecanico(mecanicoNormalizado));

        return errores;
    }

    public IReadOnlyList<Mecanico> Obtener(string? terminoBusqueda = null)
    {
        if (string.IsNullOrWhiteSpace(terminoBusqueda))
        {
            return _mecanicoRepository.GetAll();
        }

        return _mecanicoRepository.Search(terminoBusqueda);
    }

    public IReadOnlyList<Mecanico> ObtenerPorId(int id)
    {
        var mecanico = _mecanicoRepository.GetById(id);
        return mecanico is null ? [] : [mecanico];
    }

    public Task<IReadOnlyList<SugerenciaMecanico>> SugerirAsync(
        string? termino, CancellationToken cancellationToken = default, bool incluirEspecialidad = true)
    {
        var terminoNormalizado = NormalizarEspacios(termino);
        if (terminoNormalizado.Length < MinimoCaracteresSugerencias ||
            terminoNormalizado.Length > MaximoCaracteresSugerencias)
        {
            return Task.FromResult<IReadOnlyList<SugerenciaMecanico>>([]);
        }

        return _consultaMecanicos.SugerirAsync(terminoNormalizado, cancellationToken, incluirEspecialidad);
    }

    public (
        bool Actualizado,
        IReadOnlyDictionary<string, string> Errores) Actualizar(
            int id,
            MecanicoInputModel mecanicoInput)
    {
        Mecanico? existente = _mecanicoRepository.GetById(id);

        if (existente is null)
        {
            return (false, new Dictionary<string, string>());
        }

        var mecanicoNormalizado = NormalizarEntrada(mecanicoInput);
        var errores = ObtenerErrores(mecanicoNormalizado, id);

        if (errores.Count > 0)
        {
            return (false, errores);
        }

        var mecanico = CrearMecanico(mecanicoNormalizado);
        mecanico.Id = id;
        mecanico.CreadoPor = existente.CreadoPor;
        mecanico.FechaCreacion = existente.FechaCreacion;
        mecanico.ModificadoPor = UsuarioActual();
        mecanico.FechaModificacion = DateTime.Now;

        _mecanicoRepository.Update(mecanico);
        return (true, errores);
    }

    public string? Eliminar(int id)
    {
        if (_mecanicoRepository.GetById(id) is null)
        {
            return "El mecánico seleccionado ya no existe.";
        }

        try { _mecanicoRepository.Delete(id); }
        catch (MySqlConnector.MySqlException error) when (error.Number == 1451)
        { return "No se puede eliminar el mecánico porque tiene órdenes asociadas."; }
        return null;
    }

    private Dictionary<string, string> ObtenerErrores(
        MecanicoInputModel mecanico,
        int idExcluido = 0)
    {
        var errores = new Dictionary<string, string>(
            _validacionMecanicos.Validar(mecanico));

        if (errores.Count > 0)
        {
            return errores;
        }

        if (_mecanicoRepository.ExistsByCi(
                mecanico.Ci,
                mecanico.ComplementoCi,
                idExcluido))
        {
            errores[nameof(MecanicoInputModel.Ci)] = MensajeCiDuplicado;
        }

        return errores;
    }

    private static MecanicoInputModel NormalizarEntrada(MecanicoInputModel mecanicoInput)
    {
        return new MecanicoInputModel
        {
            Ci = NormalizarCi(mecanicoInput.Ci),
            ComplementoCi = NormalizarComplementoCi(mecanicoInput.ComplementoCi),
            Nombres = NormalizarNombre(mecanicoInput.Nombres),
            PrimerApellido = NormalizarNombre(mecanicoInput.PrimerApellido),
            SegundoApellido = NormalizarNombre(mecanicoInput.SegundoApellido),
            Genero = (mecanicoInput.Genero ?? string.Empty).Trim(),
            Especialidad = (mecanicoInput.Especialidad ?? string.Empty).Trim(),
            Celular = (mecanicoInput.Celular ?? string.Empty).Trim()
        };
    }

    private static string NormalizarCi(string? ci)
    {
        return (ci ?? string.Empty).Trim();
    }

    private static string NormalizarComplementoCi(string? complementoCi)
    {
        return (complementoCi ?? string.Empty).ToUpperInvariant();
    }

    private static string NormalizarEspacios(string? texto)
    {
        var textoSinEspaciosExternos = (texto ?? string.Empty).Trim();
        return EspaciosConsecutivos.Replace(textoSinEspaciosExternos, " ");
    }

    private static string NormalizarNombre(string? nombre)
    {
        var nombreSinEspaciosInnecesarios = NormalizarEspacios(nombre);
        var palabras = nombreSinEspaciosInnecesarios.Split(
            ' ',
            StringSplitOptions.RemoveEmptyEntries);

        return string.Join(
            " ",
            palabras.Select(palabra =>
                $"{char.ToUpperInvariant(palabra[0])}{palabra[1..].ToLowerInvariant()}"));
    }

    private Mecanico CrearMecanico(MecanicoInputModel mecanicoInput)
    {
        return new Mecanico
        {
            Ci = mecanicoInput.Ci,
            ComplementoCi = mecanicoInput.ComplementoCi,
            Nombres = mecanicoInput.Nombres,
            PrimerApellido = mecanicoInput.PrimerApellido,
            SegundoApellido = mecanicoInput.SegundoApellido ?? string.Empty,
            Genero = mecanicoInput.Genero,
            Especialidad = mecanicoInput.Especialidad,
            Celular = mecanicoInput.Celular,
            CreadoPor = UsuarioActual(),
            FechaCreacion = DateTime.Now
        };
    }

    private string UsuarioActual()
    {
        return string.IsNullOrWhiteSpace(_currentUser?.Username)
            ? "sistema"
            : _currentUser!.Username!;
    }
}
