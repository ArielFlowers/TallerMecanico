using System.Text;
using TallerMecanico.Services;

namespace TallerMecanico.Infraestructura.Setup;

public sealed class RestablecimientoAdminConsola
{
    private readonly InicializadorUsuarios _inicializador;

    public RestablecimientoAdminConsola(
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

        Console.WriteLine("=== RESTABLECER ADMINISTRADOR ===");
        Console.WriteLine(
            "Se cambiará únicamente la contraseña del usuario admin.");

        Console.Write("Escriba CONFIRMAR para continuar: ");

        if (Console.ReadLine() != "CONFIRMAR")
        {
            Console.WriteLine("Operación cancelada.");
            return;
        }

        Console.Write("Nueva contraseña: ");
        string password = LeerPassword();

        try
        {
            _inicializador.RestablecerPasswordAdministrador(password);

            Console.WriteLine(
                "Contraseña del administrador actualizada correctamente.");
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