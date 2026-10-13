using TallerMecanico.Application.Ordenes;
using TallerMecanico.Application.Ports;
using TallerMecanico.Models;

namespace TallerMecanico.Services;

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
