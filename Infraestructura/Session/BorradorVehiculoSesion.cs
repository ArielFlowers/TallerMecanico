using System.Text.Json;
using TallerMecanico.Application.Ports;
using TallerMecanico.ViewModels;

namespace TallerMecanico.Infraestructura.Session;

public sealed class BorradorVehiculoSesion(
    IHttpContextAccessor httpContextAccessor,
    ICurrentUser currentUser) : IBorradorVehiculoPort
{
    private ISession Session => httpContextAccessor.HttpContext?.Session
        ?? throw new InvalidOperationException("No hay una sesión disponible.");

    private static string Clave(Guid id) => $"Vehiculos:Borrador:{id:N}";

    public Guid Guardar(VehiculoFormViewModel formulario, string modal, string? buscar)
    {
        if (!currentUser.EstaAutenticado || string.IsNullOrWhiteSpace(currentUser.Username))
            throw new UnauthorizedAccessException("Debes iniciar sesión.");

        Guid id = Guid.NewGuid();
        var borrador = new BorradorVehiculo
        {
            Formulario = formulario,
            Modal = modal,
            Buscar = buscar,
            Usuario = currentUser.Username,
            VenceUtc = DateTime.UtcNow.AddMinutes(30)
        };
        Session.SetString(Clave(id), JsonSerializer.Serialize(borrador));
        return id;
    }

    public BorradorVehiculo? Obtener(Guid id)
    {
        string? contenido = Session.GetString(Clave(id));
        if (contenido is null) return null;

        BorradorVehiculo? borrador = JsonSerializer.Deserialize<BorradorVehiculo>(contenido);
        if (borrador is null || borrador.VenceUtc <= DateTime.UtcNow ||
            !currentUser.EstaAutenticado || borrador.Usuario != currentUser.Username)
        {
            Eliminar(id);
            return null;
        }

        return borrador;
    }

    public bool AsignarCliente(Guid id, int clienteId)
    {
        BorradorVehiculo? borrador = Obtener(id);
        if (borrador is null || clienteId <= 0) return false;

        borrador.Formulario.ClienteId = clienteId;
        Session.SetString(Clave(id), JsonSerializer.Serialize(borrador));
        return true;
    }

    public void Eliminar(Guid id) => Session.Remove(Clave(id));
}
