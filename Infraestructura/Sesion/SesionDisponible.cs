using StackExchange.Redis;

namespace TallerMecanico.Infraestructura.Sesion;

public static class SesionDisponible
{
    internal static readonly object ClaveUso = new();

    public static ISession Obtener(HttpContext? contexto)
    {
        if (contexto is null) throw new InvalidOperationException("No hay sesión disponible.");
        contexto.Items[ClaveUso] = true;
        try
        {
            // Los puertos existentes son síncronos. LoadAsync propaga los fallos
            // que la carga implícita de ISession ocultaría como una sesión vacía.
            contexto.Session.LoadAsync(contexto.RequestAborted).GetAwaiter().GetResult();
            return contexto.Session;
        }
        catch (Exception error) when (EsFalloAlmacenamiento(error, contexto))
        {
            throw new SesionNoDisponibleException(error);
        }
    }

    internal static bool EsFalloAlmacenamiento(Exception error, HttpContext contexto) =>
        error is RedisException or TimeoutException or ObjectDisposedException
        || error is OperationCanceledException && !contexto.RequestAborted.IsCancellationRequested;
}

internal sealed class SesionNoDisponibleException(Exception causa)
    : Exception("No se pudo cargar el estado de la sesión.", causa);
