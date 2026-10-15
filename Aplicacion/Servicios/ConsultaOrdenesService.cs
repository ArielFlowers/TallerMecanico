using TallerMecanico.Aplicacion.CasosUso.Ordenes;
using TallerMecanico.Aplicacion.Puertos;
using TallerMecanico.Dominio.Entidades;

namespace TallerMecanico.Aplicacion.Servicios;

public sealed class ConsultaOrdenesService(
    IConsultaOrdenesPort consultas,
    ICurrentUser usuario)
{
    public IReadOnlyList<OrdenResumen> Obtener(
        EstadoOrden? estado = null,
        DateOnly? fecha = null)
    {
        VerificarAutorizacion();
        return consultas.Obtener(estado, fecha);
    }

    public OrdenDetalle? ObtenerDetalle(int id)
    {
        VerificarAutorizacion();
        return id > 0 ? consultas.ObtenerDetalle(id) : null;
    }

    private void VerificarAutorizacion()
    {
        if (!usuario.EstaAutenticado
            || string.IsNullOrWhiteSpace(usuario.Username)
            || usuario.Rol is not ("Administrador" or "Recepcionista"))
        {
            throw new UnauthorizedAccessException("No tienes permiso para consultar órdenes.");
        }
    }
}
