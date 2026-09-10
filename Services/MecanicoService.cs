using System.Text.RegularExpressions;
using TallerMecanico.Data;
using TallerMecanico.Models;
using TallerMecanico.Validators;
using TallerMecanico.ViewModels;

namespace TallerMecanico.Services;

public class MecanicoService
{
    private const char SeparadorComplementoCi = '-';
    private const string MensajeCiDuplicado =
        "Ya existe un mecánico registrado con este CI.";

    private static readonly Regex EspaciosConsecutivos = new(@"\s+", RegexOptions.Compiled);

    private readonly IMecanicoRepository _mecanicoRepository;
    private readonly ValidacionMecanicos _validacionMecanicos;

    public MecanicoService(
        IMecanicoRepository mecanicoRepository,
        ValidacionMecanicos validacionMecanicos)
    {
        _mecanicoRepository = mecanicoRepository;
        _validacionMecanicos = validacionMecanicos;
    }

    public async Task<(
        int? IdMecanico,
        IReadOnlyDictionary<string, string> Errores)> CrearAsync(
            MecanicoInputModel mecanicoInput)
    {
        var mecanicoNormalizado = NormalizarEntrada(mecanicoInput);
        var errores = await ObtenerErroresAsync(mecanicoNormalizado);

        if (errores.Count > 0)
        {
            return (null, errores);
        }

        var mecanico = CrearMecanico(mecanicoNormalizado);
        var idMecanico = await _mecanicoRepository.CrearAsync(mecanico);

        return (idMecanico, errores);
    }

    public Task<IReadOnlyList<Mecanico>> ObtenerAsync(
        string? terminoBusqueda = null)
    {
        return _mecanicoRepository.ObtenerAsync(terminoBusqueda);
    }

    public async Task<(
        bool Actualizado,
        IReadOnlyDictionary<string, string> Errores)> ActualizarAsync(
            int id,
            MecanicoInputModel mecanicoInput)
    {
        var mecanicoNormalizado = NormalizarEntrada(mecanicoInput);
        var errores = await ObtenerErroresAsync(mecanicoNormalizado, id);

        if (errores.Count > 0)
        {
            return (false, errores);
        }

        var mecanico = CrearMecanico(mecanicoNormalizado);
        mecanico.Id = id;

        var actualizado = await _mecanicoRepository.ActualizarAsync(mecanico);
        return (actualizado, errores);
    }

    public Task<bool> EliminarAsync(int id)
    {
        return _mecanicoRepository.EliminarAsync(id);
    }

    private async Task<Dictionary<string, string>> ObtenerErroresAsync(
        MecanicoInputModel mecanico,
        int? idExcluido = null)
    {
        var errores = new Dictionary<string, string>(
            _validacionMecanicos.Validar(mecanico));

        if (errores.Count > 0)
        {
            return errores;
        }

        var existeCi = idExcluido.HasValue
            ? await _mecanicoRepository.ExisteCiAsync(mecanico.Ci, idExcluido.Value)
            : await _mecanicoRepository.ExisteCiAsync(mecanico.Ci);

        if (existeCi)
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
            NombreCompleto = NormalizarEspacios(mecanicoInput.NombreCompleto),
            Especialidad = NormalizarEspacios(mecanicoInput.Especialidad),
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

    private static Mecanico CrearMecanico(MecanicoInputModel mecanicoInput)
    {
        return new Mecanico
        {
            Ci = mecanicoInput.Ci,
            NombreCompleto = mecanicoInput.NombreCompleto,
            Especialidad = mecanicoInput.Especialidad,
            Celular = mecanicoInput.Celular
        };
    }
}
