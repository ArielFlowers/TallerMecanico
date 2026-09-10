using TallerMecanico.ViewModels;

namespace TallerMecanico.Validators;

public class ValidacionMecanicos
{
    private const int LongitudMinimaBaseCi = 5;
    private const int LongitudMaximaBaseCi = 8;
    private const int LongitudMinimaComplementoCi = 1;
    private const int LongitudMaximaComplementoCi = 2;
    private const int LongitudMaximaNombreCompleto = 100;
    private const int LongitudMaximaEspecialidad = 60;
    private const int LongitudCelular = 8;

    private const char SeparadorComplementoCi = '-';
    private const char PrimerPrefijoCelularPermitido = '6';
    private const char SegundoPrefijoCelularPermitido = '7';

    public IReadOnlyDictionary<string, string> Validar(MecanicoInputModel mecanico)
    {
        var errores = new Dictionary<string, string>();

        ValidarCi(mecanico.Ci, errores);
        ValidarNombreCompleto(mecanico.NombreCompleto, errores);
        ValidarEspecialidad(mecanico.Especialidad, errores);
        ValidarCelular(mecanico.Celular, errores);

        return errores;
    }

    private static void ValidarCi(string? ci, IDictionary<string, string> errores)
    {
        if (string.IsNullOrWhiteSpace(ci))
        {
            errores[nameof(MecanicoInputModel.Ci)] = "El CI es obligatorio.";
            return;
        }

        if (ContieneEspacios(ci))
        {
            errores[nameof(MecanicoInputModel.Ci)] =
                "El CI no debe contener espacios.";
            return;
        }

        if (!TieneFormatoCiValido(ci))
        {
            errores[nameof(MecanicoInputModel.Ci)] =
                $"El CI debe tener entre {LongitudMinimaBaseCi} y {LongitudMaximaBaseCi} dígitos y puede incluir un complemento de {LongitudMinimaComplementoCi} a {LongitudMaximaComplementoCi} caracteres alfanuméricos separado por un guion.";
        }
    }

    private static bool TieneFormatoCiValido(string ci)
    {
        var partes = ci.Split(SeparadorComplementoCi);

        if (partes.Length is < 1 or > 2)
        {
            return false;
        }

        var baseCi = partes[0];
        if (baseCi.Length < LongitudMinimaBaseCi ||
            baseCi.Length > LongitudMaximaBaseCi ||
            !baseCi.All(char.IsAsciiDigit))
        {
            return false;
        }

        return partes.Length == 1 || TieneComplementoCiValido(partes[1]);
    }

    private static bool TieneComplementoCiValido(string complemento)
    {
        return complemento.Length >= LongitudMinimaComplementoCi &&
               complemento.Length <= LongitudMaximaComplementoCi &&
               complemento.All(char.IsAsciiLetterOrDigit);
    }

    private static void ValidarNombreCompleto(
        string? nombreCompleto,
        IDictionary<string, string> errores)
    {
        if (string.IsNullOrWhiteSpace(nombreCompleto))
        {
            errores[nameof(MecanicoInputModel.NombreCompleto)] =
                "El nombre completo es obligatorio.";
            return;
        }

        if (nombreCompleto.Length > LongitudMaximaNombreCompleto)
        {
            errores[nameof(MecanicoInputModel.NombreCompleto)] =
                $"El nombre completo no debe superar los {LongitudMaximaNombreCompleto} caracteres.";
            return;
        }

        if (!SoloContieneLetrasYEspacios(nombreCompleto))
        {
            errores[nameof(MecanicoInputModel.NombreCompleto)] =
                "El nombre completo debe contener únicamente letras y espacios.";
        }
    }

    private static void ValidarEspecialidad(
        string? especialidad,
        IDictionary<string, string> errores)
    {
        if (string.IsNullOrWhiteSpace(especialidad))
        {
            errores[nameof(MecanicoInputModel.Especialidad)] =
                "La especialidad es obligatoria.";
            return;
        }

        if (especialidad.Length > LongitudMaximaEspecialidad)
        {
            errores[nameof(MecanicoInputModel.Especialidad)] =
                $"La especialidad no debe superar los {LongitudMaximaEspecialidad} caracteres.";
            return;
        }

        if (!SoloContieneLetrasYEspacios(especialidad))
        {
            errores[nameof(MecanicoInputModel.Especialidad)] =
                "La especialidad debe contener únicamente letras y espacios.";
        }
    }

    private static bool SoloContieneLetrasYEspacios(string texto)
    {
        return texto.All(caracter => char.IsLetter(caracter) || caracter == ' ');
    }

    private static bool ContieneEspacios(string texto)
    {
        return texto.Any(char.IsWhiteSpace);
    }

    private static void ValidarCelular(string? celular, IDictionary<string, string> errores)
    {
        if (string.IsNullOrWhiteSpace(celular))
        {
            errores[nameof(MecanicoInputModel.Celular)] = "El celular es obligatorio.";
            return;
        }

        if (ContieneEspacios(celular))
        {
            errores[nameof(MecanicoInputModel.Celular)] =
                "El celular no debe contener espacios.";
            return;
        }

        if (celular.Length != LongitudCelular)
        {
            errores[nameof(MecanicoInputModel.Celular)] =
                $"El celular debe tener exactamente {LongitudCelular} dígitos.";
            return;
        }

        if (!celular.All(char.IsAsciiDigit))
        {
            errores[nameof(MecanicoInputModel.Celular)] =
                "El celular debe contener únicamente números.";
            return;
        }

        if (!TienePrefijoCelularValido(celular))
        {
            errores[nameof(MecanicoInputModel.Celular)] =
                $"El celular debe comenzar con {PrimerPrefijoCelularPermitido} o {SegundoPrefijoCelularPermitido}.";
        }
    }

    private static bool TienePrefijoCelularValido(string celular)
    {
        return celular[0] == PrimerPrefijoCelularPermitido ||
               celular[0] == SegundoPrefijoCelularPermitido;
    }

}
