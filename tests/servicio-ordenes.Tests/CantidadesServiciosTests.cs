using System.Globalization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Primitives;
using TallerMecanico.Aplicacion.CasosUso.Ordenes;
using TallerMecanico.Aplicacion.CasosUso.Ordenes.Contratos;
using TallerMecanico.Aplicacion.Servicios;
using TallerMecanico.Aplicacion.Validaciones;

namespace TallerMecanico.IntegrationTests;

[Trait("Area", "Backend")]
public sealed class CantidadesServiciosTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CantidadesMalformadas_DevuelvenErrorSinAbrirTransaccion(bool entradaNula)
    {
        IReadOnlyList<LineaServicioOrdenSolicitud> cantidades = entradaNula
            ? [null!]
            : [new(1, 1), new(1, 3)];
        var solicitud = new CrearOrdenSolicitud(1, 1, DateTime.Today, Guid.NewGuid(), [],
            ServicioIds: [1], CantidadesServicios: cantidades);
        var servicio = new OrdenServicioPrueba(new UnidadNoUtilizable(), new UsuarioPrueba(),
            new ValidacionOrdenes(), TimeProvider.System);

        var resultado = servicio.CrearOrden(solicitud);

        Assert.False(resultado.Exito);
        Assert.Contains(nameof(CrearOrdenSolicitud.CantidadesServicios), resultado.Errores);
    }

    [Fact]
    public void ServiciosDuplicados_SeRechazanAunqueSusCantidadesSeanValidas()
    {
        var solicitud = new CrearOrdenSolicitud(1, 1, DateTime.Today, Guid.NewGuid(), [],
            ServicioIds: [1, 1], CantidadesServicios: [new(1, 1)]);

        Assert.Contains(nameof(CrearOrdenSolicitud.ServicioIds), new ValidacionOrdenes().Validar(solicitud));
    }


    private sealed class UnidadNoUtilizable : IUnidadTrabajoOrden
    {
        public ITransaccionOrden Iniciar() => throw new InvalidOperationException("No debe abrirse una transacción.");
    }
}
