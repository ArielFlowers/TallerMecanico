using TallerMecanico.ViewModels;

namespace TallerMecanico.Validators;

public class ValidacionServicios
{
    private const int LongitudMaximaNombre = 100;
    private const int LongitudMaximaDescripcion = 300;

    private const string CaracteresEspecialesNombre = "-/().";
    private const string CaracteresEspecialesDescripcion = ",:;%+";

    public IReadOnlyDictionary<string, string> Validar(
        ServicioFormViewModel servicio)
    {
        var errores = new Dictionary<string, string>();

        ValidarNombre(servicio.Nombre, errores);
        ValidarDescripcion(servicio.Descripcion, errores);

        return errores;
    }

    private static void ValidarNombre(
        string? nombre,
        IDictionary<string, string> errores)
    {
        const string campo = nameof(ServicioFormViewModel.Nombre);

        if (string.IsNullOrWhiteSpace(nombre))
        {
            return;
        }

        if (TieneEspaciosEnExtremos(nombre))
        {
            errores[campo] =
                "El nombre no puede comenzar ni terminar con espacios.";
            return;
        }

        if (TieneEspaciosConsecutivos(nombre))
        {
            errores[campo] =
                "El nombre solo puede contener un espacio entre palabras.";
            return;
        }

        if (nombre.Length > LongitudMaximaNombre)
        {
            errores[campo] =
                $"El nombre no debe superar los {LongitudMaximaNombre} caracteres.";
            return;
        }

        if (!nombre.All(EsCaracterValidoEnNombre))
        {
            errores[campo] =
                "El nombre contiene caracteres no permitidos.";
        }
    }

    private static void ValidarDescripcion(
        string? descripcion,
        IDictionary<string, string> errores)
    {
        const string campo = nameof(ServicioFormViewModel.Descripcion);

        if (string.IsNullOrWhiteSpace(descripcion))
        {
            return;
        }

        if (TieneEspaciosEnExtremos(descripcion))
        {
            errores[campo] =
                "La descripción no puede comenzar ni terminar con espacios.";
            return;
        }

        if (TieneEspaciosConsecutivos(descripcion))
        {
            errores[campo] =
                "La descripción solo puede contener un espacio entre palabras.";
            return;
        }

        if (descripcion.Length > LongitudMaximaDescripcion)
        {
            errores[campo] =
                $"La descripción no debe superar los {LongitudMaximaDescripcion} caracteres.";
            return;
        }

        if (!descripcion.All(EsCaracterValidoEnDescripcion))
        {
            errores[campo] =
                "La descripción contiene caracteres no permitidos.";
        }
    }

    private static bool TieneEspaciosEnExtremos(string texto)
    {
        return texto.StartsWith(' ') || texto.EndsWith(' ');
    }

    private static bool TieneEspaciosConsecutivos(string texto)
    {
        return texto.Contains("  ");
    }

    private static bool EsCaracterValidoEnNombre(char caracter)
    {
        return char.IsLetterOrDigit(caracter) ||
               caracter == ' ' ||
               CaracteresEspecialesNombre.Contains(caracter);
    }

    private static bool EsCaracterValidoEnDescripcion(char caracter)
    {
        return EsCaracterValidoEnNombre(caracter) ||
               CaracteresEspecialesDescripcion.Contains(caracter);
    }
}