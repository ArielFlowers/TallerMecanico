using TallerMecanico.ViewModels;

namespace TallerMecanico.Validators;

public class ValidacionMecanicos
{
    private const int LongitudMinimaBaseCi = 5;
    private const int LongitudMaximaBaseCi = 8;
    private const int LongitudComplementoCi = 2;
    private const int LongitudMaximaNombresYApellidos = 100;
    private const int LongitudCelular = 8;

    private const char PrimerPrefijoCelularPermitido = '5';
    private const char SegundoPrefijoCelularPermitido = '6';
    private const char TercerPrefijoCelularPermitido = '7';

    public IReadOnlyDictionary<string, string> Validar(MecanicoInputModel mecanico)
    {
        var errores = new Dictionary<string, string>();

        ValidarCi(mecanico.Ci, errores);
        ValidarComplementoCi(mecanico.ComplementoCi, errores);
        ValidarNombres(mecanico.Nombres, errores);
        ValidarPrimerApellido(mecanico.PrimerApellido, errores);
        ValidarSegundoApellido(mecanico.SegundoApellido, errores);
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

        if (ci.Length < LongitudMinimaBaseCi || ci.Length > LongitudMaximaBaseCi)
        {
            errores[nameof(MecanicoInputModel.Ci)] =
                $"El CI debe tener entre {LongitudMinimaBaseCi} y {LongitudMaximaBaseCi} dígitos.";
            return;
        }

        if (!ci.All(char.IsAsciiDigit))
        {
            errores[nameof(MecanicoInputModel.Ci)] =
                "El CI debe contener únicamente números.";
        }
    }

    private static void ValidarComplementoCi(
        string? complementoCi,
        IDictionary<string, string> errores)
    {
        if (string.IsNullOrEmpty(complementoCi))
        {
            return;
        }

        if (ContieneEspacios(complementoCi))
        {
            errores[nameof(MecanicoInputModel.ComplementoCi)] =
                "El complemento del CI no debe contener espacios.";
            return;
        }

        if (complementoCi.Length != LongitudComplementoCi)
        {
            errores[nameof(MecanicoInputModel.ComplementoCi)] =
                $"El complemento del CI debe tener exactamente {LongitudComplementoCi} caracteres.";
            return;
        }

        if (!TieneFormatoComplementoCiValido(complementoCi))
        {
            errores[nameof(MecanicoInputModel.ComplementoCi)] =
                "El complemento debe tener el formato 1A: un número seguido de una letra.";
        }
    }

    private static bool TieneFormatoComplementoCiValido(string complementoCi)
    {
        return char.IsAsciiDigit(complementoCi[0]) && complementoCi[1] is >= 'A' and <= 'Z';
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

    private static void ValidarPrimerApellido(
        string? primerApellido,
        IDictionary<string, string> errores)
    {
        if (string.IsNullOrWhiteSpace(primerApellido))
        {
            errores[nameof(MecanicoInputModel.PrimerApellido)] =
                "El primer apellido es obligatorio.";
            return;
        }

        if (primerApellido.Length > LongitudMaximaNombresYApellidos)
        {
            errores[nameof(MecanicoInputModel.PrimerApellido)] =
                $"El primer apellido no puede superar los {LongitudMaximaNombresYApellidos} caracteres.";
            return;
        }

        if (!SoloContieneLetrasYEspacios(primerApellido))
        {
            errores[nameof(MecanicoInputModel.PrimerApellido)] =
                "El primer apellido solo puede contener letras y espacios.";
        }
    }

    private static void ValidarSegundoApellido(
        string? segundoApellido,
        IDictionary<string, string> errores)
    {
        if (string.IsNullOrWhiteSpace(segundoApellido))
        {
            errores[nameof(MecanicoInputModel.SegundoApellido)] =
                "El segundo apellido es obligatorio.";
            return;
        }

        if (segundoApellido.Length > LongitudMaximaNombresYApellidos)
        {
            errores[nameof(MecanicoInputModel.SegundoApellido)] =
                $"El segundo apellido no puede superar los {LongitudMaximaNombresYApellidos} caracteres.";
            return;
        }

        if (!SoloContieneLetrasYEspacios(segundoApellido))
        {
            errores[nameof(MecanicoInputModel.SegundoApellido)] =
                "El segundo apellido solo puede contener letras y espacios.";
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
                $"El celular debe comenzar en {PrimerPrefijoCelularPermitido}, {SegundoPrefijoCelularPermitido} o {TercerPrefijoCelularPermitido}.";
        }
    }

    private static bool TienePrefijoCelularValido(string celular)
    {
        return celular[0] == PrimerPrefijoCelularPermitido ||
               celular[0] == SegundoPrefijoCelularPermitido ||
               celular[0] == TercerPrefijoCelularPermitido;
    }

}
