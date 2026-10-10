
using System.Text;
using TallerMecanico.Services;

namespace TallerMecanico.Infraestructura.Setup;

public sealed class InicializacionUsuariosConsola
{
    private readonly InicializadorUsuarios _inicializador;

    public InicializacionUsuariosConsola(
        InicializadorUsuarios inicializador)
    {
        _inicializador = inicializador;
    }

    public void Ejecutar()
    {
        if (Console.IsInputRedirected ||
            Console.IsOutputRedirected)
        {
            throw new InvalidOperationException(
                "Esta operación requiere una consola interactiva.");
        }

        Console.WriteLine("=== INICIALIZACION DE USUARIOS ===");
        Console.WriteLine(
            "Esta operación insertará cuentas en MySQL.");

        Console.Write("Escriba CONFIRMAR para continuar: ");

        if (Console.ReadLine() != "CONFIRMAR")
        {
            Console.WriteLine("Operación cancelada.");
            return;
        }

        CrearCuenta("Administrador");
        CrearCuenta("Recepcionista");

        Console.WriteLine(
            "Inicialización de usuarios finalizada.");
    }

    private void CrearCuenta(string rol)
    {
        Console.WriteLine();
        Console.WriteLine($"Cuenta: {rol}");

        Console.Write("Nombre de usuario: ");

        string username =
            Console.ReadLine() ?? string.Empty;

        Console.Write("Contraseña: ");

        string password = LeerPassword();

        try
        {
            _inicializador.CrearUsuarioInicial(
                username,
                password,
                rol);

            Console.WriteLine(
                $"Cuenta de {rol} creada correctamente.");
        }
        finally
        {
            password = string.Empty;
        }
    }

    private static string LeerPassword()
    {
        var caracteres = new StringBuilder();

        while (true)
        {
            ConsoleKeyInfo tecla =
                Console.ReadKey(intercept: true);

            if (tecla.Key == ConsoleKey.Enter)
            {
                Console.WriteLine();
                return caracteres.ToString();
            }

            if (tecla.Key == ConsoleKey.Backspace)
            {
                if (caracteres.Length > 0)
                {
                    caracteres.Length--;
                }

                continue;
            }

            if (!char.IsControl(tecla.KeyChar) &&
                caracteres.Length < 128)
            {
                caracteres.Append(tecla.KeyChar);
            }
        }
    }
}
