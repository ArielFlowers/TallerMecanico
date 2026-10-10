using Microsoft.AspNetCore.Http;
using TallerMecanico.Application.Ports;
using TallerMecanico.Models;

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

internal sealed class ClientesPrueba : IClientePort
{
    public List<Cliente> Datos { get; } = [];
    public IReadOnlyList<Cliente> GetAll() => Datos;
    public Cliente? GetById(int id) => Datos.Find(cliente => cliente.Id == id);
    public IReadOnlyList<Cliente> Search(string terminoBusqueda) => Datos;
    public bool ExistsByCi(string ci, string complementoCi, int idExcluido = 0) =>
        Datos.Any(cliente => cliente.Ci == ci && cliente.ComplementoCi == complementoCi && cliente.Id != idExcluido);
    public void Add(Cliente cliente) { cliente.Id = Datos.Count + 1; Datos.Add(cliente); }
    public void Update(Cliente cliente) { Datos.RemoveAll(actual => actual.Id == cliente.Id); Datos.Add(cliente); }
    public void Delete(int id) => Datos.RemoveAll(cliente => cliente.Id == id);
}

internal sealed class VehiculosPrueba : IVehiculoPort
{
    public List<Vehiculo> Datos { get; } = [];
    public IReadOnlyList<Vehiculo> GetAll() => Datos;
    public Vehiculo? GetById(int id) => Datos.Find(vehiculo => vehiculo.Id == id);
    public IReadOnlyList<Vehiculo> Search(string filtro) => Datos;
    public bool ExistsByPlaca(string placa, int idExcluido = 0) =>
        Datos.Any(vehiculo => vehiculo.Placa == placa && vehiculo.Id != idExcluido);
    public bool ExistsPorCliente(int clienteId) => Datos.Any(vehiculo => vehiculo.ClienteId == clienteId);
    public void Add(Vehiculo vehiculo) { vehiculo.Id = Datos.Count + 1; Datos.Add(vehiculo); }
    public void Update(Vehiculo vehiculo) { Datos.RemoveAll(actual => actual.Id == vehiculo.Id); Datos.Add(vehiculo); }
    public void Delete(int id) => Datos.RemoveAll(vehiculo => vehiculo.Id == id);
}
