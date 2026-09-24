using System.Text.RegularExpressions;
using TallerMecanico.Data;
using TallerMecanico.Models;
using TallerMecanico.Patterns.FactoryMethod;
using TallerMecanico.Validators;
using TallerMecanico.ViewModels;

namespace TallerMecanico.Services;

public class MecanicoService
{
    private const char SeparadorComplementoCi = '-';
    private const string MensajeCiDuplicado =
        "Ya existe un mecánico registrado con este CI.";

    private static readonly Regex EspaciosConsecutivos = new(@"\s+", RegexOptions.Compiled);

    private readonly MecanicoRepository _mecanicoRepository;
    private readonly ValidacionMecanicos _validacionMecanicos;

    public MecanicoService(
        CreadorMecanico creadorMecanico,
        ValidacionMecanicos validacionMecanicos)
    {
        _mecanicoRepository = (MecanicoRepository)creadorMecanico.CrearRepositorio();
        _validacionMecanicos = validacionMecanicos;
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

    public (
        bool Actualizado,
        IReadOnlyDictionary<string, string> Errores) Actualizar(
            int id,
            MecanicoInputModel mecanicoInput)
    {
        if (_mecanicoRepository.GetById(id) is null)
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

        _mecanicoRepository.Update(mecanico);
        return (true, errores);
    }

    public bool Eliminar(int id)
    {
        if (_mecanicoRepository.GetById(id) is null)
        {
            return false;
        }

        _mecanicoRepository.Delete(id);
        return true;
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

        if (_mecanicoRepository.ExistsByCi(mecanico.Ci, idExcluido))
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
            Nombres = NormalizarNombre(mecanicoInput.Nombres),
            Apellidos = NormalizarNombre(mecanicoInput.Apellidos),
            Genero = (mecanicoInput.Genero ?? string.Empty).Trim(),
            Especialidad = (mecanicoInput.Especialidad ?? string.Empty).Trim(),
            Celular = (mecanicoInput.Celular ?? string.Empty).Trim()
        };
    }

    private static string NormalizarCi(string? ci)
    {
        var ciSinEspaciosExternos = (ci ?? string.Empty).Trim();
        var indiceSeparador = ciSinEspaciosExternos.IndexOf(SeparadorComplementoCi);

        if (indiceSeparador < 0)
        {
            return ciSinEspaciosExternos;
        }

        var parteBaseConSeparador = ciSinEspaciosExternos[..(indiceSeparador + 1)];
        var complemento = ciSinEspaciosExternos[(indiceSeparador + 1)..]
            .ToUpperInvariant();

        return $"{parteBaseConSeparador}{complemento}";
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

    private static Mecanico CrearMecanico(MecanicoInputModel mecanicoInput)
    {
        return new Mecanico
        {
            Ci = mecanicoInput.Ci,
            Nombres = mecanicoInput.Nombres,
            Apellidos = mecanicoInput.Apellidos,
            Genero = mecanicoInput.Genero,
            Especialidad = mecanicoInput.Especialidad,
            Celular = mecanicoInput.Celular
        };
    }
}
