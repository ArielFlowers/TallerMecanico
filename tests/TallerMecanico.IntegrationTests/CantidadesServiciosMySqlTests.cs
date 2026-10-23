using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging.Abstractions;
using TallerMecanico.Aplicacion.CasosUso.Ordenes;
using TallerMecanico.Aplicacion.Puertos;
using TallerMecanico.Aplicacion.Servicios;
using TallerMecanico.Aplicacion.Validaciones;
using TallerMecanico.Infraestructura.Persistencia.MySql;

namespace TallerMecanico.IntegrationTests;

public sealed class CantidadesServiciosMySqlTests
{
    [MySqlAisladoFact]
    public void Repetible_CantidadTres_GuardaSubtotalHistoricoSinDescontarProductos()
    {
        using var aislada = new MySqlAisladoFixture();
        NuevaOrdenAutocompletadoTests.Preparar(aislada);
        aislada.Ejecutar("UPDATE Servicios SET UnidadCantidad = 'unidad' WHERE Id = 1;");

        var resultado = CrearServicio(aislada).CrearOrden(Solicitud(3));

        Assert.True(resultado.Exito, string.Join("; ", resultado.Errores.Values));
        Assert.Equal(75m, resultado.Total);
        Assert.Equal(3, Convert.ToInt32(aislada.Escalar(
            "SELECT Cantidad FROM DetalleServiciosOrdenes WHERE OrdenId = @Id;", ("@Id", resultado.OrdenId))));
        Assert.Equal("unidad", aislada.Escalar(
            "SELECT UnidadCantidad FROM DetalleServiciosOrdenes WHERE OrdenId = @Id;", ("@Id", resultado.OrdenId)));
        Assert.Equal(75m, Convert.ToDecimal(aislada.Escalar(
            "SELECT Costo FROM DetalleServiciosOrdenes WHERE OrdenId = @Id;", ("@Id", resultado.OrdenId))));
        Assert.Equal(20, Convert.ToInt32(aislada.Escalar("SELECT SUM(Stock) FROM Productos;")));
        Assert.Equal(0, Convert.ToInt32(aislada.Escalar("SELECT COUNT(*) FROM DetalleOrdenes;")));
    }

    [MySqlAisladoFact]
    public void CabeceraTransaccional_RecuperaUnidadNulaYTodasLasUnidadesPermitidas()
    {
        using var aislada = new MySqlAisladoFixture();
        NuevaOrdenAutocompletadoTests.Preparar(aislada);
        foreach (string? unidad in new string?[] { null, "unidad", "llanta", "retrovisor" })
        {
            aislada.Ejecutar("UPDATE Servicios SET UnidadCantidad = @Unidad WHERE Id = 1;", ("@Unidad", unidad));
            using var transaccion = Unidad(aislada).Iniciar();

            Assert.True(transaccion.Cabecera.ExisteServicio(1, out var servicio));
            Assert.NotNull(servicio);
            Assert.Equal(unidad, servicio.UnidadCantidad);
            Assert.Equal(25m, servicio.Costo);
            Assert.Equal(1m, servicio.TiempoEstimadoHoras);
            Assert.False(transaccion.Cabecera.ExisteServicio(int.MaxValue, out _));
        }
    }

    [MySqlAisladoFact]
    public void Repetible_AdmiteCantidadUnoYLimiteMil()
    {
        using var aislada = new MySqlAisladoFixture();
        PrepararRepetible(aislada);
        foreach (int cantidad in new[] { 1, 1000 })
        {
            var resultado = CrearServicio(aislada).CrearOrden(Solicitud(cantidad));

            Assert.True(resultado.Exito, string.Join("; ", resultado.Errores.Values));
            Assert.Equal(25m * cantidad, resultado.Total);
            Assert.Equal(cantidad, Convert.ToInt32(aislada.Escalar(
                "SELECT Cantidad FROM DetalleServiciosOrdenes WHERE OrdenId = @Id;", ("@Id", resultado.OrdenId))));
        }
        Assert.Equal(20, Convert.ToInt32(aislada.Escalar("SELECT SUM(Stock) FROM Productos;")));
    }

    [MySqlAisladoFact]
    public void Unico_AdmiteUnoYLaCantidadOmitidaConservaCompatibilidad()
    {
        using var aislada = new MySqlAisladoFixture();
        NuevaOrdenAutocompletadoTests.Preparar(aislada);
        foreach (var solicitud in new[] { Solicitud(1), Solicitud(1) with { CantidadesServicios = null } })
        {
            var resultado = CrearServicio(aislada).CrearOrden(solicitud);

            Assert.True(resultado.Exito, string.Join("; ", resultado.Errores.Values));
            Assert.Equal(25m, resultado.Total);
            Assert.Equal(DBNull.Value, aislada.Escalar(
                "SELECT UnidadCantidad FROM DetalleServiciosOrdenes WHERE OrdenId = @Id;", ("@Id", resultado.OrdenId)));
            Assert.Equal(1, Convert.ToInt32(aislada.Escalar(
                "SELECT Cantidad FROM DetalleServiciosOrdenes WHERE OrdenId = @Id;", ("@Id", resultado.OrdenId))));
        }
    }

    [MySqlAisladoFact]
    public void CantidadesCeroNegativasSuperioresAlLimiteYUnicoConDos_NoEscriben()
    {
        using var aislada = new MySqlAisladoFixture();
        PrepararRepetible(aislada);
        string antes = NuevaOrdenAutocompletadoTests.Instantanea(aislada);
        foreach (int cantidad in new[] { 0, -1, 1001 })
        {
            var resultado = CrearServicio(aislada).CrearOrden(Solicitud(cantidad));
            Assert.False(resultado.Exito);
            Assert.Contains("Servicios[1]", resultado.Errores);
        }
        aislada.Ejecutar("UPDATE Servicios SET UnidadCantidad = NULL WHERE Id = 1;");
        Assert.Contains("Servicios[1]", CrearServicio(aislada).CrearOrden(Solicitud(2)).Errores);
        Assert.Equal(antes, NuevaOrdenAutocompletadoTests.Instantanea(aislada));
        Assert.Equal(0, Convert.ToInt32(aislada.Escalar("SELECT COUNT(*) FROM DetalleServiciosOrdenes;")));
    }

    [MySqlAisladoFact]
    public void ServiciosDuplicadosYCantidadesMalformadas_DevuelvenErroresSinEscribir()
    {
        using var aislada = new MySqlAisladoFixture();
        PrepararRepetible(aislada);
        string antes = NuevaOrdenAutocompletadoTests.Instantanea(aislada);
        var solicitudes = new[]
        {
            Solicitud(1) with { ServicioIds = [1, 1] },
            Solicitud(1) with { CantidadesServicios = [new(1, 1), new(1, 3)] },
            Solicitud(1) with { CantidadesServicios = [null!] },
            Solicitud(1) with { CantidadesServicios = [new(2, 1)] }
        };
        foreach (var solicitud in solicitudes)
        {
            var resultado = CrearServicio(aislada).CrearOrden(solicitud);
            Assert.False(resultado.Exito);
            Assert.NotEmpty(resultado.Errores);
        }
        Assert.Equal(antes, NuevaOrdenAutocompletadoTests.Instantanea(aislada));
        Assert.Equal(0, Convert.ToInt32(aislada.Escalar("SELECT COUNT(*) FROM DetalleServiciosOrdenes;")));
    }

    [MySqlAisladoFact]
    public async Task Fracciones_RechazadasPorElModelBinderNoLleganAlGuardado()
    {
        using var aislada = new MySqlAisladoFixture();
        PrepararRepetible(aislada);
        foreach (string cantidad in new[] { "1.5", "1,5" })
        {
            var pagina = NuevaOrdenAutocompletadoTests.CrearPagina(aislada);
            pagina.OnGet();
            Guid token = pagina.Formulario.Token;
            var (formulario, errores) = await CantidadesServiciosTests.EnlazarCantidadAsync(cantidad);
            formulario.Token = token;
            pagina.Formulario = formulario;
            pagina.ModelState.Merge(errores);

            Assert.IsType<PageResult>(pagina.OnPostGuardar());
            Assert.False(pagina.ModelState.IsValid);
            Assert.NotEmpty(pagina.ModelState["Formulario.CantidadesServicios[1]"]!.Errors);
        }
        Assert.Equal(0, Convert.ToInt32(aislada.Escalar("SELECT COUNT(*) FROM Ordenes;")));
        Assert.Equal(20, Convert.ToInt32(aislada.Escalar("SELECT SUM(Stock) FROM Productos;")));
    }

    [MySqlAisladoFact]
    public void BorradorYErrorDeFormulario_ConservanCantidadHastaGuardar()
    {
        using var aislada = new MySqlAisladoFixture();
        PrepararRepetible(aislada);
        var sesion = new SesionPrueba();
        var pagina = NuevaOrdenAutocompletadoTests.CrearPagina(aislada, sesion);
        pagina.OnGet();
        pagina.Formulario.VehiculoId = 1;
        pagina.Formulario.MecanicoId = 1;
        pagina.Formulario.ServicioIds = [1];
        pagina.Formulario.CantidadesServicios[1] = 3;
        var salida = Assert.IsType<RedirectToPageResult>(pagina.OnPostNuevoCliente());
        var restaurada = NuevaOrdenAutocompletadoTests.CrearPagina(aislada, sesion);
        restaurada.BorradorId = (Guid)salida.RouteValues!["OrdenBorradorId"]!;

        Assert.IsType<PageResult>(restaurada.OnGet());
        Assert.Equal(3, restaurada.Formulario.CantidadesServicios[1]);
        Assert.Equal(75m, restaurada.TotalEstimado);
        restaurada.ModelState.AddModelError("Formulario.VehiculoId", "Error de prueba.");
        Assert.IsType<PageResult>(restaurada.OnPostGuardar());
        Assert.Equal(3, Assert.Single(restaurada.ServiciosSeleccionados).Cantidad);
        Assert.Equal(0, Convert.ToInt32(aislada.Escalar("SELECT COUNT(*) FROM Ordenes;")));
        restaurada.ModelState.Clear();

        Assert.IsType<RedirectToPageResult>(restaurada.OnPostGuardar());
        Assert.Equal(3, Convert.ToInt32(aislada.Escalar("SELECT Cantidad FROM DetalleServiciosOrdenes;")));
        Assert.Equal(75m, Convert.ToDecimal(aislada.Escalar("SELECT Total FROM Ordenes;")));
    }

    [MySqlAisladoFact]
    public void PreciosMySqlTotalesEHistorico_ConservanIdempotenciaYStockIndependiente()
    {
        using var aislada = new MySqlAisladoFixture();
        PrepararRepetible(aislada);
        aislada.Ejecutar("UPDATE Servicios SET Costo = 30 WHERE Id = 1;");
        int unicoId = checked((int)aislada.Ejecutar("""
            INSERT INTO Servicios (Nombre, Descripcion, Costo, TiempoEstimadoHoras)
            VALUES ('Diagnóstico de prueba', 'Servicio único de prueba', 120, 1);
            """));
        string json = JsonSerializer.Serialize(new
        {
            VehiculoId = 1, MecanicoId = 1, Fecha = DateTime.Today, Token = Guid.NewGuid(), Total = 0.01m,
            ServicioIds = new[] { 1, unicoId },
            CantidadesServicios = new[]
            {
                new { ServicioId = 1, Cantidad = 3, Costo = 0.01m, UnidadCantidad = (string?)null },
                new { ServicioId = unicoId, Cantidad = 1, Costo = 0.01m, UnidadCantidad = (string?)"unidad" }
            },
            Lineas = new[] { new { ProductoId = 1, Cantidad = 2, PrecioUnitario = 0.01m, Subtotal = 0.02m } }
        });
        var solicitud = JsonSerializer.Deserialize<CrearOrdenSolicitud>(json)!;

        var resultado = CrearServicio(aislada).CrearOrden(solicitud);

        Assert.True(resultado.Exito, string.Join("; ", resultado.Errores.Values));
        Assert.Equal(235m, resultado.Total);
        Assert.Equal(235m, Convert.ToDecimal(aislada.Escalar("SELECT Total FROM Ordenes;")));
        Assert.Equal(90m, Convert.ToDecimal(aislada.Escalar("SELECT CostoServicio FROM Ordenes;")));
        Assert.Equal(210m, Convert.ToDecimal(aislada.Escalar("SELECT SUM(Costo) FROM DetalleServiciosOrdenes;")));
        Assert.Equal(25m, Convert.ToDecimal(aislada.Escalar("SELECT SUM(Subtotal) FROM DetalleOrdenes;")));
        Assert.Equal(18, Convert.ToInt32(aislada.Escalar("SELECT SUM(Stock) FROM Productos;")));
        aislada.Ejecutar("UPDATE Servicios SET Costo = 999, UnidadCantidad = NULL WHERE Id = 1;");

        var detalle = new ConsultaOrdenesRepository(aislada.Factory).ObtenerDetalle(resultado.OrdenId!.Value);
        Assert.NotNull(detalle);
        Assert.Equal(235m, detalle.Total);
        Assert.Equal(2, detalle.Servicios.Count);
        Assert.Equal(90m, detalle.Servicios[0].Costo);
        Assert.Equal(3, detalle.Servicios[0].Cantidad);
        Assert.Equal("unidad", detalle.Servicios[0].UnidadCantidad);
        Assert.Equal(120m, detalle.Servicios[1].Costo);
        Assert.Equal(1, detalle.Servicios[1].Cantidad);
        Assert.Null(detalle.Servicios[1].UnidadCantidad);
        Assert.Equal(2, Convert.ToInt32(aislada.Escalar("SELECT COUNT(*) FROM HistorialCostoServicios WHERE ServicioId = 1;")));

        var reenvio = CrearServicio(aislada).CrearOrden(solicitud);
        Assert.True(reenvio.EsReenvio);
        Assert.Equal(resultado.OrdenId, reenvio.OrdenId);
        Assert.Equal(235m, reenvio.Total);
        Assert.Equal(1, Convert.ToInt32(aislada.Escalar("SELECT COUNT(*) FROM Ordenes;")));
        Assert.Equal(18, Convert.ToInt32(aislada.Escalar("SELECT SUM(Stock) FROM Productos;")));

        var administrador = new UsuarioPrueba { Rol = "Administrador" };
        Assert.True(CrearServicio(aislada, administrador).AnularOrden(resultado.OrdenId.Value).Exito);
        Assert.Equal(20, Convert.ToInt32(aislada.Escalar("SELECT SUM(Stock) FROM Productos;")));
        Assert.Equal(210m, Convert.ToDecimal(aislada.Escalar("SELECT SUM(Costo) FROM DetalleServiciosOrdenes;")));
    }

    [MySqlAisladoFact]
    public void FalloTrasDescontarProducto_RevierteOrdenServiciosYStockYPermiteReintento()
    {
        using var aislada = new MySqlAisladoFixture();
        PrepararRepetible(aislada);
        var solicitud = Solicitud(3) with { Lineas = [new(1, 2)] };
        var interceptada = new UnidadTrabajoInterceptada(Unidad(aislada), despues: (operacion, cuenta) =>
        {
            if (operacion == "Descontar" && cuenta == 1)
                throw new PersistenciaOrdenException("Fallo de prueba tras el descuento real.");
        });

        Assert.False(CrearServicio(aislada, unidad: interceptada).CrearOrden(solicitud).Exito);
        Assert.Equal(0, Convert.ToInt32(aislada.Escalar("SELECT COUNT(*) FROM Ordenes;")));
        Assert.Equal(0, Convert.ToInt32(aislada.Escalar("SELECT COUNT(*) FROM DetalleServiciosOrdenes;")));
        Assert.Equal(0, Convert.ToInt32(aislada.Escalar("SELECT COUNT(*) FROM DetalleOrdenes;")));
        Assert.Equal(20, Convert.ToInt32(aislada.Escalar("SELECT SUM(Stock) FROM Productos;")));

        var reintento = CrearServicio(aislada).CrearOrden(solicitud);
        Assert.True(reintento.Exito, string.Join("; ", reintento.Errores.Values));
        Assert.Equal(100m, reintento.Total);
        Assert.Equal(18, Convert.ToInt32(aislada.Escalar("SELECT SUM(Stock) FROM Productos;")));
    }

    private static void PrepararRepetible(MySqlAisladoFixture aislada)
    {
        NuevaOrdenAutocompletadoTests.Preparar(aislada);
        aislada.Ejecutar("UPDATE Servicios SET UnidadCantidad = 'unidad' WHERE Id = 1;");
    }

    private static CrearOrdenSolicitud Solicitud(int cantidad) => new(
        1, 1, DateTime.Today, Guid.NewGuid(), [], ServicioIds: [1],
        CantidadesServicios: [new(1, cantidad)]);

    private static MySqlUnidadTrabajoOrden Unidad(MySqlAisladoFixture aislada) =>
        new(aislada.Factory, NullLogger<MySqlUnidadTrabajoOrden>.Instance);

    private static OrdenServicioService CrearServicio(MySqlAisladoFixture aislada,
        UsuarioPrueba? usuario = null, IUnidadTrabajoOrdenPort? unidad = null) => new(
        unidad ?? Unidad(aislada), usuario ?? new UsuarioPrueba(), new ValidacionOrdenes(), TimeProvider.System,
        NullLogger<OrdenServicioService>.Instance);
}
