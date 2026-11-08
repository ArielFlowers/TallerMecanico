using Microsoft.AspNetCore.Http;
using TallerMecanico.Seguridad;

namespace TallerMecanico.IntegrationTests;

internal sealed class UsuarioPrueba : ICurrentUser
{
    public bool EstaAutenticado { get; set; } = true;
    public int? UsuarioId { get; set; } = 1;
    public string? Username { get; set; } = "recepcion_prueba";
    public string? Rol { get; set; } = "Recepcionista";
}

internal sealed class SesionPrueba : ISession
{
    private readonly Dictionary<string, byte[]> _datos = [];
    public bool IsAvailable => true;
    public string Id => "sesion-prueba";
    public IEnumerable<string> Keys => _datos.Keys;
    public void Clear() => _datos.Clear();
    public void Remove(string key) => _datos.Remove(key);
    public void Set(string key, byte[] value) => _datos[key] = value;
    public bool TryGetValue(string key, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out byte[]? value) => _datos.TryGetValue(key, out value);
    public Task LoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
}
