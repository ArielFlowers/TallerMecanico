using System.Text.Json;
using TallerMecanico.Aplicacion.Puertos;
using TallerMecanico.Aplicacion.Dto;

namespace TallerMecanico.Infraestructura.Sesion;

public sealed class BorradorOrdenSesion(IHttpContextAccessor contexto, ICurrentUser usuario) : IBorradorOrdenPort
{
    private ISession Session => contexto.HttpContext?.Session ?? throw new InvalidOperationException("No hay sesión disponible.");
    private static string Clave(Guid id) => $"Ordenes:Borrador:{id:N}";

    public Guid Guardar(OrdenFormViewModel formulario)
    {
        if (!usuario.EstaAutenticado || string.IsNullOrWhiteSpace(usuario.Username))
            throw new UnauthorizedAccessException("Debes iniciar sesión.");
        Guid id = Guid.NewGuid();
        Session.SetString(Clave(id), JsonSerializer.Serialize(new Sobre(usuario.Username, DateTime.UtcNow.AddMinutes(30), formulario)));
        return id;
    }

    public OrdenFormViewModel? Obtener(Guid id)
    {
        string? json = Session.GetString(Clave(id));
        if (json is null) return null;
        Sobre? sobre = JsonSerializer.Deserialize<Sobre>(json);
        if (sobre is null || sobre.VenceUtc <= DateTime.UtcNow || sobre.Usuario != usuario.Username)
        {
            Eliminar(id);
            return null;
        }
        return sobre.Formulario;
    }

    public void Eliminar(Guid id) => Session.Remove(Clave(id));

    private sealed record Sobre(string Usuario, DateTime VenceUtc, OrdenFormViewModel Formulario);
}
