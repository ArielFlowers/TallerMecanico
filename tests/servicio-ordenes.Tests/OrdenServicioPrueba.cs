using Microsoft.Extensions.Logging.Abstractions;
using TallerMecanico.Aplicacion.CasosUso.Ordenes;
using TallerMecanico.Aplicacion.CasosUso.Ordenes.Contratos;
using TallerMecanico.Aplicacion.Fachadas;
using TallerMecanico.Aplicacion.Puertos;
using TallerMecanico.Aplicacion.Validaciones;

namespace TallerMecanico.IntegrationTests;

// Agrupa los casos de uso de escritura para no repetir su construcción en cada prueba.
internal sealed class OrdenServicioPrueba(
    IUnidadTrabajoOrden unidad,
    ICurrentUser usuario,
    ValidacionOrdenes validador,
    TimeProvider reloj,
    IAuditoriaPort? auditoria = null)
{
    private readonly CrearOrdenCasoUso _crear =
        new(unidad, usuario, validador, reloj, NullLogger<CrearOrdenCasoUso>.Instance);
    private readonly AnularOrdenCasoUso _anular =
        new(unidad, usuario, reloj, NullLogger<AnularOrdenCasoUso>.Instance, auditoria);

    public DateTime FechaActual => _crear.FechaActual;

    public ResultadoOrden CrearOrden(CrearOrdenSolicitud solicitud) => _crear.Ejecutar(solicitud);

    public ResultadoOrden AnularOrden(int ordenId) => _anular.Ejecutar(ordenId);

    public OrdenServicioFacade CrearFacade() => new(_crear, _anular);
}
