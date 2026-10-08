using TallerMecanico.ViewModels;

namespace TallerMecanico.Validators;

public class ValidacionClientes
{
    private const int LongitudMinimaBaseCi = 5;
    private const int LongitudMaximaBaseCi = 8;
    private const int LongitudComplementoCi = 2;
    private const int LongitudMaximaNombresYApellidos = 100;
    private const int LongitudCelular = 8;

    private const char PrimerPrefijoCelularPermitido = '6';
    private const char SegundoPrefijoCelularPermitido = '7';

    public IReadOnlyDictionary<string, string> Validar(
        ClienteFormViewModel cliente)
    {
        var errores = new Dictionary<string, string>();

        ValidarCi(cliente.Ci, errores);
        ValidarComplementoCi(cliente.ComplementoCi, errores);
        ValidarNombres(cliente.Nombres, errores);
        ValidarPrimerApellido(cliente.PrimerApellido, errores);
        ValidarSegundoApellido(cliente.SegundoApellido, errores);
        ValidarCelular(cliente.Celular, errores);

        return errores;
    }

    private static void ValidarCi(
        string? ci,
        IDictionary<string, string> errores)
    {
        if (string.IsNullOrWhiteSpace(ci))
        {
            errores[nameof(ClienteFormViewModel.Ci)] =
                "El CI es obligatorio.";

            return;
        }

        if (ContieneEspacios(ci))
        {
            errores[nameof(ClienteFormViewModel.Ci)] =
                "El CI no debe contener espacios.";

            return;
        }

        if (ci.Length < LongitudMinimaBaseCi ||
            ci.Length > LongitudMaximaBaseCi)
        {
            errores[nameof(ClienteFormViewModel.Ci)] =
                $"El CI debe tener entre {LongitudMinimaBaseCi} y {LongitudMaximaBaseCi} dígitos.";

            return;
        }

        if (!ci.All(char.IsAsciiDigit))
        {
            errores[nameof(ClienteFormViewModel.Ci)] =
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
            errores[nameof(ClienteFormViewModel.ComplementoCi)] =
                "El complemento del CI no debe contener espacios.";

            return;
        }

        if (complementoCi.Length != LongitudComplementoCi)
        {
            errores[nameof(ClienteFormViewModel.ComplementoCi)] =
                $"El complemento del CI debe tener exactamente {LongitudComplementoCi} caracteres.";

            return;
        }

        if (!TieneFormatoComplementoCiValido(complementoCi))
        {
            errores[nameof(ClienteFormViewModel.ComplementoCi)] =
                "El complemento debe tener el formato 1A: un número seguido de una letra.";
        }
    }

    private static bool TieneFormatoComplementoCiValido(
        string complementoCi)
    {
        return
            char.IsAsciiDigit(complementoCi[0]) &&
            complementoCi[1] is >= 'A' and <= 'Z';
    }

    private static void ValidarNombres(
        string? nombres,
        IDictionary<string, string> errores)
    {
        if (string.IsNullOrWhiteSpace(nombres))
        {
            errores[nameof(ClienteFormViewModel.Nombres)] =
                "Los nombres son obligatorios.";

            return;
        }

        if (nombres.Length > LongitudMaximaNombresYApellidos)
        {
            errores[nameof(ClienteFormViewModel.Nombres)] =
                $"Los nombres no pueden superar los {LongitudMaximaNombresYApellidos} caracteres.";

            return;
        }

        if (!SoloContieneLetrasYEspacios(nombres))
        {
            errores[nameof(ClienteFormViewModel.Nombres)] =
                "Los nombres solo pueden contener letras y espacios.";
        }
    }

    private static void ValidarPrimerApellido(
        string? primerApellido,
        IDictionary<string, string> errores)
    {
        if (string.IsNullOrWhiteSpace(primerApellido))
        {
            errores[nameof(ClienteFormViewModel.PrimerApellido)] =
                "El primer apellido es obligatorio.";

            return;
        }

        if (primerApellido.Length > LongitudMaximaNombresYApellidos)
        {
            errores[nameof(ClienteFormViewModel.PrimerApellido)] =
                $"El primer apellido no puede superar los {LongitudMaximaNombresYApellidos} caracteres.";

            return;
        }

        if (!SoloContieneLetrasYEspacios(primerApellido))
        {
            errores[nameof(ClienteFormViewModel.PrimerApellido)] =
                "El primer apellido solo puede contener letras y espacios.";
        }
    }

    private static void ValidarSegundoApellido(
        string? segundoApellido,
        IDictionary<string, string> errores)
    {
        if (string.IsNullOrWhiteSpace(segundoApellido))
        {
            errores[nameof(ClienteFormViewModel.SegundoApellido)] =
                "El segundo apellido es obligatorio.";

            return;
        }

        if (segundoApellido.Length > LongitudMaximaNombresYApellidos)
        {
            errores[nameof(ClienteFormViewModel.SegundoApellido)] =
                $"El segundo apellido no puede superar los {LongitudMaximaNombresYApellidos} caracteres.";

            return;
        }

        if (!SoloContieneLetrasYEspacios(segundoApellido))
        {
            errores[nameof(ClienteFormViewModel.SegundoApellido)] =
                "El segundo apellido solo puede contener letras y espacios.";
        }
    }

    private static void ValidarCelular(
        string? celular,
        IDictionary<string, string> errores)
    {
        if (string.IsNullOrWhiteSpace(celular))
        {
            errores[nameof(ClienteFormViewModel.Celular)] =
                "El celular es obligatorio.";

            return;
        }

        if (ContieneEspacios(celular))
        {
            errores[nameof(ClienteFormViewModel.Celular)] =
                "El celular no debe contener espacios.";

            return;
        }

        if (celular.Length != LongitudCelular)
        {
            errores[nameof(ClienteFormViewModel.Celular)] =
                $"El celular debe tener exactamente {LongitudCelular} dígitos.";

            return;
        }

        if (!celular.All(char.IsAsciiDigit))
        {
            errores[nameof(ClienteFormViewModel.Celular)] =
                "El celular debe contener únicamente números.";

            return;
        }

        if (!TienePrefijoCelularValido(celular))
        {
            errores[nameof(ClienteFormViewModel.Celular)] =
                $"El celular debe comenzar en {PrimerPrefijoCelularPermitido} o {SegundoPrefijoCelularPermitido}.";
        }
    }

    private static bool SoloContieneLetrasYEspacios(
        string texto)
    {
        return texto.All(
            caracter =>
                char.IsLetter(caracter) ||
                caracter == ' ');
    }

    private static bool ContieneEspacios(
        string texto)
    {
        return texto.Any(char.IsWhiteSpace);
    }

    private static bool TienePrefijoCelularValido(
        string celular)
    {
        return
            celular[0] == PrimerPrefijoCelularPermitido ||
            celular[0] == SegundoPrefijoCelularPermitido;
    }
}