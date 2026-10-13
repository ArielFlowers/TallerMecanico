
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Konscious.Security.Cryptography;
using Microsoft.Extensions.Options;
using TallerMecanico.Application.Ports;

namespace TallerMecanico.Infraestructura.Identity;

public sealed class Argon2idPasswordHasher : IPasswordHasher
{
    private const int SaltSize = 16;
    private const int HashSize = 32;

    private readonly Argon2idOptions _options;

    public Argon2idPasswordHasher(
        IOptions<Argon2idOptions> options)
    {
        _options = options.Value;

        ValidarParametros(
            _options.MemorySize,
            _options.Iterations,
            _options.DegreeOfParallelism);
    }

    public string GenerarHash(string password)
    {
        if (string.IsNullOrWhiteSpace(password))
        {
            throw new ArgumentException(
                "La contraseña no puede estar vacía.",
                nameof(password));
        }

        byte[] salt = RandomNumberGenerator.GetBytes(SaltSize);

        byte[] hash = CalcularHash(
            password,
            salt,
            _options.MemorySize,
            _options.Iterations,
            _options.DegreeOfParallelism);

        return FormatearHash(salt, hash);
    }

    public bool VerificarPassword(
        string password,
        string passwordHash)
    {
        if (string.IsNullOrEmpty(password) ||
            string.IsNullOrEmpty(passwordHash))
        {
            return false;
        }

        if (!IntentarLeerHash(
                passwordHash,
                out byte[] salt,
                out byte[] hashOriginal,
                out int memoria,
                out int iteraciones,
                out int paralelismo))
        {
            return false;
        }

        byte[] hashCalculado = CalcularHash(
            password,
            salt,
            memoria,
            iteraciones,
            paralelismo);

        try
        {
            return CryptographicOperations.FixedTimeEquals(
                hashCalculado,
                hashOriginal);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(hashCalculado);
        }
    }

    private static byte[] CalcularHash(
        string password,
        byte[] salt,
        int memoria,
        int iteraciones,
        int paralelismo)
    {
        byte[] passwordBytes = Encoding.UTF8.GetBytes(password);

        try
        {
            using var argon2 = new Argon2id(passwordBytes)
            {
                Salt = salt,
                MemorySize = memoria,
                Iterations = iteraciones,
                DegreeOfParallelism = paralelismo
            };

            return argon2.GetBytes(HashSize);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(passwordBytes);
        }
    }

    private string FormatearHash(
        byte[] salt,
        byte[] hash)
    {
        return
            $"$argon2id$v=19$" +
            $"m={_options.MemorySize}," +
            $"t={_options.Iterations}," +
            $"p={_options.DegreeOfParallelism}$" +
            $"{Convert.ToBase64String(salt)}$" +
            $"{Convert.ToBase64String(hash)}";
    }

    private static bool IntentarLeerHash(
        string passwordHash,
        out byte[] salt,
        out byte[] hash,
        out int memoria,
        out int iteraciones,
        out int paralelismo)
    {
        salt = [];
        hash = [];
        memoria = 0;
        iteraciones = 0;
        paralelismo = 0;

        if (passwordHash.Length > 255)
            return false;

        string[] partes = passwordHash.Split('$');

        if (partes.Length != 6 ||
            partes[0] != string.Empty ||
            partes[1] != "argon2id" ||
            partes[2] != "v=19")
        {
            return false;
        }

        string[] parametros = partes[3].Split(',');

        if (parametros.Length != 3 ||
            !LeerEntero(parametros[0], "m=", out memoria) ||
            !LeerEntero(parametros[1], "t=", out iteraciones) ||
            !LeerEntero(parametros[2], "p=", out paralelismo))
        {
            return false;
        }

        if (!ParametrosValidos(
                memoria,
                iteraciones,
                paralelismo))
        {
            return false;
        }

        try
        {
            salt = Convert.FromBase64String(partes[4]);
            hash = Convert.FromBase64String(partes[5]);

            return salt.Length == SaltSize &&
                   hash.Length == HashSize;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static bool LeerEntero(
        string valor,
        string prefijo,
        out int resultado)
    {
        resultado = 0;

        return valor.StartsWith(
                   prefijo,
                   StringComparison.Ordinal)
               &&
               int.TryParse(
                   valor[prefijo.Length..],
                   NumberStyles.None,
                   CultureInfo.InvariantCulture,
                   out resultado);
    }

    private static bool ParametrosValidos(
        int memoria,
        int iteraciones,
        int paralelismo)
    {
        return memoria >= 8192 &&
               memoria <= 131072 &&
               iteraciones >= 1 &&
               iteraciones <= 10 &&
               paralelismo >= 1 &&
               paralelismo <= 4 &&
               memoria >= 8 * paralelismo;
    }

    private static void ValidarParametros(
        int memoria,
        int iteraciones,
        int paralelismo)
    {
        if (!ParametrosValidos(
                memoria,
                iteraciones,
                paralelismo))
        {
            throw new ArgumentOutOfRangeException(
                nameof(memoria),
                "Los parámetros de Argon2id no son válidos.");
        }
    }
}
