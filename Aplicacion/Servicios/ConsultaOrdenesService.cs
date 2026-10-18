using TallerMecanico.Aplicacion.CasosUso.Ordenes;
using TallerMecanico.Aplicacion.Puertos;
using TallerMecanico.Dominio.Entidades;

namespace TallerMecanico.Aplicacion.Servicios;

public sealed class ConsultaOrdenesService(
    IConsultaOrdenesPort consultas,
    ICurrentUser usuario)
{
    private const int MinimoCaracteresSugerencias = 2;
    private const int MaximoCaracteresSugerencias = 100;

    public IReadOnlyList<OrdenResumen> Obtener(
        EstadoOrden? estado = null,
        DateOnly? fecha = null,
        string? terminoBusqueda = null,
        int? ordenSeleccionadaId = null)
    {
        VerificarAutorizacion();
        return consultas.Obtener(estado, fecha, terminoBusqueda?.Trim(), ordenSeleccionadaId);
    }

    public Task<IReadOnlyList<SugerenciaOrden>> SugerirAsync(
        string? termino,
        EstadoOrden? estado = null,
        DateOnly? fecha = null,
        CancellationToken cancellationToken = default)
    {
        VerificarAutorizacion();
        var terminoNormalizado = termino?.Trim() ?? string.Empty;
        if (terminoNormalizado.Length < MinimoCaracteresSugerencias ||
            terminoNormalizado.Length > MaximoCaracteresSugerencias)
        {
            return Task.FromResult<IReadOnlyList<SugerenciaOrden>>([]);
        }

        return consultas.SugerirAsync(terminoNormalizado, estado, fecha, cancellationToken);
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
