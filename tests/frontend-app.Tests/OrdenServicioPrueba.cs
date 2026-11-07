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
    TimeProvider reloj)
{
    private readonly CrearOrdenCasoUso _crear =
        new(unidad, usuario, validador, reloj, NullLogger<CrearOrdenCasoUso>.Instance);
    private readonly AnularOrdenCasoUso _anular =
        new(unidad, usuario, reloj, NullLogger<AnularOrdenCasoUso>.Instance);

    public DateTime FechaActual => _crear.FechaActual;

    public ResultadoOrden CrearOrden(CrearOrdenSolicitud solicitud) => _crear.Ejecutar(solicitud);

    public ResultadoOrden AnularOrden(int ordenId) => _anular.Ejecutar(ordenId);

    public FachadaConTokenPrueba CrearFacade(ITokenOrden tokens) => new(this, tokens);
}

// La pertenencia del token se prueba en el PageModel del Frontend; el backend
// conserva la transacción y la idempotencia. Este adaptador solo prepara su solicitud.
internal sealed class FachadaConTokenPrueba(OrdenServicioPrueba servicio, ITokenOrden tokens)
{
    public Guid GenerarTokenFormulario() => tokens.Generar();
    public ResultadoOrden CrearOrden(CrearOrdenSolicitud solicitud) => servicio.CrearOrden(solicitud);
}
