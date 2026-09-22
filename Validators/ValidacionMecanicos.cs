using TallerMecanico.ViewModels;

namespace TallerMecanico.Validators;

public class ValidacionMecanicos
{
    private const int LongitudMinimaBaseCi = 5;
    private const int LongitudMaximaBaseCi = 8;
    private const int LongitudMinimaComplementoCi = 1;
    private const int LongitudMaximaComplementoCi = 2;
    private const int LongitudMaximaNombresYApellidos = 100;
    private const int LongitudCelular = 8;

    private const char SeparadorComplementoCi = '-';
    private const char PrimerPrefijoCelularPermitido = '6';
    private const char SegundoPrefijoCelularPermitido = '7';

    public IReadOnlyDictionary<string, string> Validar(MecanicoInputModel mecanico)
    {
        var errores = new Dictionary<string, string>();

        ValidarCi(mecanico.Ci, errores);
        ValidarNombres(mecanico.Nombres, errores);
        ValidarApellidos(mecanico.Apellidos, errores);
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

    private static void ValidarNombres(
        string? nombres,
        IDictionary<string, string> errores)
    {
        if (string.IsNullOrWhiteSpace(nombres))
        {
            errores[nameof(MecanicoInputModel.Nombres)] =
                "Los nombres son obligatorios.";
            return;
        }

        if (nombres.Length > LongitudMaximaNombresYApellidos)
        {
            errores[nameof(MecanicoInputModel.Nombres)] =
                $"Los nombres no pueden superar los {LongitudMaximaNombresYApellidos} caracteres.";
            return;
        }

        if (!SoloContieneLetrasYEspacios(nombres))
        {
            errores[nameof(MecanicoInputModel.Nombres)] =
                "Los nombres solo pueden contener letras y espacios.";
        }
    }

    private static void ValidarApellidos(
        string? apellidos,
        IDictionary<string, string> errores)
    {
        if (string.IsNullOrWhiteSpace(apellidos))
        {
            errores[nameof(MecanicoInputModel.Apellidos)] =
                "Los apellidos son obligatorios.";
            return;
        }

        if (apellidos.Length > LongitudMaximaNombresYApellidos)
        {
            errores[nameof(MecanicoInputModel.Apellidos)] =
                $"Los apellidos no pueden superar los {LongitudMaximaNombresYApellidos} caracteres.";
            return;
        }

        if (!SoloContieneLetrasYEspacios(apellidos))
        {
            errores[nameof(MecanicoInputModel.Apellidos)] =
                "Los apellidos solo pueden contener letras y espacios.";
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
