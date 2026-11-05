using Microsoft.Extensions.Logging;

namespace TallerMecanico.IntegrationTests;

internal sealed class RegistradorPrueba<T> : ILogger<T>
{
    public List<string> Mensajes { get; } = [];
    public List<LogLevel> Niveles { get; } = [];
    public bool IsEnabled(LogLevel logLevel) => true;
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public void Log<TState>(LogLevel nivel, EventId evento, TState estado, Exception? error,
        Func<TState, Exception?, string> formato)
    {
        Mensajes.Add(formato(estado, error));
        Niveles.Add(nivel);
    }
}
