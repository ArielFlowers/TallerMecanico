using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using TallerMecanico.Aplicacion.CasosUso.Ordenes;
using TallerMecanico.Aplicacion.Puertos;
using TallerMecanico.Aplicacion.Servicios;
using TallerMecanico.Aplicacion.Validaciones;
using TallerMecanico.Infraestructura.Persistencia.MySql;

namespace TallerMecanico.IntegrationTests;

public sealed class NuevaOrdenFechaTests
{
    [Theory]
    [InlineData("2026-10-19T00:30:00Z", "2026-10-18")]
    [InlineData("2026-10-19T03:59:59Z", "2026-10-18")]
    [InlineData("2026-10-19T04:00:00Z", "2026-10-19")]
    public void FechaActual_UsaBoliviaIndependientementeDeLaZonaDelServidor(string utc, string fecha)
    {
        var reloj = new RelojPrueba { Ahora = DateTimeOffset.Parse(utc) };
        var servicio = new OrdenServicioService(new UnidadNoUtilizada(), new UsuarioPrueba(),
            new ValidacionOrdenes(), reloj, NullLogger<OrdenServicioService>.Instance);
        Assert.Equal(DateTime.Parse(fecha), servicio.FechaActual);
    }

    [MySqlAisladoFact]
    public void Crear_IgnoraFechaRecibidaYReenvioNoCambiaFechaHistoricaNiStock()
    {
        using var aislada = new MySqlAisladoFixture();
        NuevaOrdenAutocompletadoTests.Preparar(aislada);
        var reloj = new RelojPrueba { Ahora = DateTimeOffset.Parse("2026-10-19T03:59:59Z") };
        var servicio = new OrdenServicioService(new MySqlUnidadTrabajoOrden(aislada.Factory, NullLogger<MySqlUnidadTrabajoOrden>.Instance),
            new UsuarioPrueba(), new ValidacionOrdenes(), reloj, NullLogger<OrdenServicioService>.Instance);
        var solicitud = new CrearOrdenSolicitud(1, 1, new DateTime(1999, 1, 1), Guid.NewGuid(), [new(1, 2)], 1);
        var primera = servicio.CrearOrden(solicitud);
        Assert.True(primera.Exito);
        Assert.Equal(new DateTime(2026, 10, 18), aislada.Escalar("SELECT Fecha FROM Ordenes;"));
        reloj.Ahora = reloj.Ahora.AddSeconds(1);
        Assert.Equal(new DateTime(2026, 10, 19), servicio.FechaActual);
        var reenvio = servicio.CrearOrden(solicitud with { Fecha = default });
        Assert.True(reenvio.Exito);
        Assert.True(reenvio.EsReenvio);
        Assert.Equal(primera.OrdenId, reenvio.OrdenId);
        Assert.Equal(new DateTime(2026, 10, 18), aislada.Escalar("SELECT Fecha FROM Ordenes;"));
        Assert.Equal(8L, Convert.ToInt64(aislada.Escalar("SELECT Stock FROM Productos WHERE Id=1;")));
        Assert.Equal(1L, Convert.ToInt64(aislada.Escalar("SELECT COUNT(*) FROM Ordenes;")));
        var siguiente = servicio.CrearOrden(solicitud with { Token = Guid.NewGuid(), Fecha = default });
        Assert.True(siguiente.Exito);
        Assert.Equal(new DateTime(2026, 10, 19), aislada.Escalar("SELECT Fecha FROM Ordenes WHERE Id=@Id;", ("@Id", siguiente.OrdenId)));
    }

    [MySqlAisladoFact]
    public void BorradorAntiguoYError_MuestranFechaActualSinRestaurarUnaFechaDeEntrada()
    {
        using var aislada = new MySqlAisladoFixture();
        NuevaOrdenAutocompletadoTests.Preparar(aislada);
        var sesion = new SesionPrueba();
        var reloj = new RelojPrueba { Ahora = DateTimeOffset.Parse("2026-10-19T03:59:59Z") };
        var pagina = NuevaOrdenAutocompletadoTests.CrearPagina(aislada, sesion, reloj);
        pagina.OnGet();
        var salida = Assert.IsType<RedirectToPageResult>(pagina.OnPostNuevoCliente());
        Guid id = (Guid)salida.RouteValues!["OrdenBorradorId"]!;
        string clave = $"Ordenes:Borrador:{id:N}";
        var borrador = JsonNode.Parse(sesion.GetString(clave)!)!;
        borrador["Formulario"]!["Fecha"] = "1999-01-01T00:00:00";
        sesion.SetString(clave, borrador.ToJsonString());
        reloj.Ahora = reloj.Ahora.AddSeconds(1);
        var retorno = NuevaOrdenAutocompletadoTests.CrearPagina(aislada, sesion, reloj);
        retorno.BorradorId = id;
        Assert.IsType<Microsoft.AspNetCore.Mvc.RazorPages.PageResult>(retorno.OnGet());
        Assert.Equal(new DateTime(2026, 10, 19), retorno.FechaOrden);
        Assert.IsType<Microsoft.AspNetCore.Mvc.RazorPages.PageResult>(retorno.OnPostGuardar());
        Assert.Equal(new DateTime(2026, 10, 19), retorno.FechaOrden);
        Assert.DoesNotContain(typeof(TallerMecanico.Aplicacion.Dto.OrdenFormViewModel).GetProperties(), propiedad => propiedad.Name == "Fecha");
        Assert.Equal(0L, Convert.ToInt64(aislada.Escalar("SELECT COUNT(*) FROM Ordenes;")));
    }

    private sealed class RelojPrueba : TimeProvider
    {
        public DateTimeOffset Ahora { get; set; }
        public override DateTimeOffset GetUtcNow() => Ahora;
    }

    private sealed class UnidadNoUtilizada : IUnidadTrabajoOrdenPort
    {
        public ITransaccionOrden Iniciar() => throw new InvalidOperationException("Solo se consulta el reloj.");
    }
}
