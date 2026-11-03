using System.Globalization;
using System.Text;
using TallerMecanico.Aplicacion.Puertos;

namespace TallerMecanico.Aplicacion.Servicios;

public sealed class GeneradorNombreUsuarioService : IGeneradorNombreUsuarioPort
{
    private const int LongitudMinima = 3;
    private const int LongitudMaxima = 50;
    private const int MaximoIntentos = 100;

    private readonly IUsuarioPort _usuarios;
    private readonly ISolicitudRegistroPort _solicitudes;

    public GeneradorNombreUsuarioService(
        IUsuarioPort usuarios,
        ISolicitudRegistroPort solicitudes)
    {
        _usuarios = usuarios;
        _solicitudes = solicitudes;
    }

    public string GenerarUsuarioUnico(
        string correoElectronico,
        System.Threading.CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(correoElectronico))
        {
            throw new ArgumentException(
                "El correo electrónico es obligatorio para generar el nombre de usuario.",
                nameof(correoElectronico));
        }

        string baseUsuario = Normalizar(correoElectronico);

        if (string.IsNullOrWhiteSpace(baseUsuario) || baseUsuario.Length < LongitudMinima)
        {
            baseUsuario = "usuario";
        }

        if (baseUsuario.Length > LongitudMaxima)
        {
            baseUsuario = baseUsuario[..LongitudMaxima];
        }

        string candidato = baseUsuario;
        int sufijo = 1;

        while (NombreExiste(candidato))
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (sufijo++ > MaximoIntentos)
            {
                throw new InvalidOperationException(
                    "No se pudo generar un nombre de usuario único tras varios intentos.");
            }

            string sufijoTexto = $"-{sufijo}";
            int espacioDisponible = LongitudMaxima - sufijoTexto.Length;
            string baseRecortada =
                baseUsuario.Length > espacioDisponible
                    ? baseUsuario[..espacioDisponible]
                    : baseUsuario;

            candidato = baseRecortada + sufijoTexto;
        }

        return candidato;
    }

    private bool NombreExiste(string username) =>
        _usuarios.ExisteUsername(username) ||
        _solicitudes.ExisteUsername(username);

    private static string Normalizar(string correoElectronico)
    {
        string parteLocal = correoElectronico.Contains('@')
            ? correoElectronico.Split('@', 2)[0]
            : correoElectronico;

        parteLocal = parteLocal.Trim();
        string sinAcentos = EliminarAcentos(parteLocal).ToLowerInvariant();

        var sb = new StringBuilder();
        foreach (char c in sinAcentos)
        {
            if (c is '.' or '_' or '-' || char.IsLetterOrDigit(c))
            {
                sb.Append(c);
            }
            else if (sb.Length > 0 && sb[^1] != '-')
            {
                sb.Append('-');
            }
        }

        return sb.ToString().Trim('-');
    }

    private static string EliminarAcentos(string texto)
    {
        string normalizado = texto.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();
        foreach (char c in normalizado)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            {
                sb.Append(c);
            }
        }
        return sb.ToString();
    }
}
