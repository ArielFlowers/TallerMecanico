using System.Text.Json;
using TallerMecanico.Aplicacion.Puertos;
using TallerMecanico.Aplicacion.Dto;

namespace TallerMecanico.Infraestructura.Sesion;

public sealed class BorradorVehiculoSesion(
    IHttpContextAccessor httpContextAccessor,
    ICurrentUser currentUser) : IBorradorVehiculoPort
{
    private ISession Session => SesionDisponible.Obtener(httpContextAccessor.HttpContext);

    private static string Clave(Guid id) => $"Vehiculos:Borrador:{id:N}";

    public Guid Guardar(VehiculoFormViewModel formulario, string modal, string? buscar,
        Guid? ordenBorradorId = null, int? vehiculoSeleccionadoId = null)
    {
        if (!currentUser.EstaAutenticado || string.IsNullOrWhiteSpace(currentUser.Username))
            throw new UnauthorizedAccessException("Debes iniciar sesión.");

        Guid id = Guid.NewGuid();
        var borrador = new BorradorVehiculo
        {
            Formulario = formulario,
            Modal = modal,
            Buscar = buscar,
            VehiculoSeleccionadoId = vehiculoSeleccionadoId,
            Usuario = currentUser.Username,
            VenceUtc = DateTime.UtcNow.AddMinutes(30),
            OrdenBorradorId = ordenBorradorId
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
