using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using TallerMecanico.Application;
using TallerMecanico.Application.Ordenes;
using TallerMecanico.Application.Ports;
using TallerMecanico.Data;
using TallerMecanico.Infraestructura.Adapters.MySql;
using TallerMecanico.Infraestructura.Session;
using TallerMecanico.Models;
using TallerMecanico.Patterns.FactoryMethod;
using TallerMecanico.Services;
using TallerMecanico.Validators;

namespace TallerMecanico.IntegrationTests;

[Collection("MySQL aislado")]
public sealed class OrdenesMySqlTests(MySqlAisladoFixture basePrueba)
{
    private static int _ci = 3000000;
    private sealed record Datos(int Vehiculo, int Mecanico, int A, int B);
    private IUnidadTrabajoOrdenPort Unidad => new MySqlUnidadTrabajoOrden(basePrueba.Factory, NullLogger<MySqlUnidadTrabajoOrden>.Instance);
    private OrdenServicioService Servicio(UsuarioPrueba? usuario = null, IUnidadTrabajoOrdenPort? unidad = null) =>
        new(unidad ?? Unidad, usuario ?? new UsuarioPrueba(), new ValidacionOrdenes(), TimeProvider.System, NullLogger<OrdenServicioService>.Instance);

    private Datos Preparar(int stockA = 10, int stockB = 10)
    {
        int ci = Interlocked.Increment(ref _ci);
        int cliente = checked((int)basePrueba.Ejecutar("""
            INSERT INTO Clientes (Ci, Nombres, PrimerApellido, SegundoApellido, Celular)
            VALUES (@Ci, 'Ana', 'Arce', 'Perez', '71234567');
            """, ("@Ci", ci.ToString())));
        int vehiculo = checked((int)basePrueba.Ejecutar("""
            INSERT INTO Vehiculos (Placa, Marca, Modelo, Kilometraje, ClienteId, EsPlacaExtranjera)
            VALUES (@Placa, 'Toyota', 'Corolla', 100, @Cliente, TRUE);
            """, ("@Placa", Guid.NewGuid().ToString("N")[..15].ToUpperInvariant()), ("@Cliente", cliente)));
        int mecanico = checked((int)basePrueba.Ejecutar("""
            INSERT INTO Mecanicos (Ci, Nombres, PrimerApellido, SegundoApellido, Genero, Especialidad, Celular)
            VALUES (@Ci, 'Juan', 'Lopez', 'Perez', 'Masculino', 'Sin Especialidad', '71234567');
            """, ("@Ci", ci.ToString())));
        int a = Producto("Producto A", 12.50m, stockA);
        int b = Producto("Producto B", 7.20m, stockB);
        return new(vehiculo, mecanico, a, b);
    }

    private int Producto(string nombre, decimal precio, int stock) => checked((int)basePrueba.Ejecutar("""
        INSERT INTO Productos (Codigo, Nombre, Precio, Stock, StockMinimo)
        VALUES (@Codigo, @Nombre, @Precio, @Stock, 0);
        """, ("@Codigo", Guid.NewGuid().ToString("N")), ("@Nombre", nombre), ("@Precio", precio), ("@Stock", stock)));

    private static CrearOrdenSolicitud Solicitud(Datos datos, Guid? token = null, params LineaOrdenSolicitud[] lineas) =>
        new(datos.Vehiculo, datos.Mecanico, DateTime.Today, token ?? Guid.NewGuid(), lineas.Length == 0 ? [new LineaOrdenSolicitud(datos.A, 2), new LineaOrdenSolicitud(datos.B, 3)] : lineas);

    private int Stock(int productoId) => Convert.ToInt32(basePrueba.Escalar("SELECT Stock FROM Productos WHERE Id = @Id;", ("@Id", productoId)));
    private int ContarOrden(Guid token) => Convert.ToInt32(basePrueba.Escalar("SELECT COUNT(*) FROM Ordenes WHERE Token = @Token;", ("@Token", token.ToString("D"))));
    private int ContarDetalles() => Convert.ToInt32(basePrueba.Escalar("SELECT COUNT(*) FROM DetalleOrdenes;"));

    [MySqlAisladoFact]
    public void CrearOrden_FacadeGuardaDosProductosTotalYUsuario()
    {
        Datos datos = Preparar();
        var usuario = new UsuarioPrueba();
        var contexto = new HttpContextAccessor { HttpContext = new DefaultHttpContext { Session = new SesionPrueba() } };
        var facade = new OrdenServicioFacade(Servicio(usuario), new TokenOrdenSesion(contexto, usuario, TimeProvider.System));
        var solicitud = Solicitud(datos, facade.GenerarTokenFormulario());
        ResultadoOrden resultado = facade.CrearOrden(solicitud);
        Assert.True(resultado.Exito, string.Join("; ", resultado.Errores.Values));
        Assert.Equal(46.60m, resultado.Total);
        Assert.Equal(8, Stock(datos.A));
        Assert.Equal(7, Stock(datos.B));
        Assert.Equal(2, Convert.ToInt32(basePrueba.Escalar("SELECT COUNT(*) FROM DetalleOrdenes WHERE OrdenId = @Id;", ("@Id", resultado.OrdenId))));
        Assert.Equal(resultado.Total, Convert.ToDecimal(basePrueba.Escalar("SELECT SUM(Subtotal) FROM DetalleOrdenes WHERE OrdenId = @Id;", ("@Id", resultado.OrdenId))));
        Assert.Equal(usuario.Username, basePrueba.Escalar("SELECT CreadoPor FROM Ordenes WHERE Id = @Id;", ("@Id", resultado.OrdenId)));
        Assert.Equal(EstadoOrden.Activa, resultado.Estado);
        Assert.Equal(resultado.OrdenId, facade.CrearOrden(solicitud).OrdenId);
        Assert.Equal(8, Stock(datos.A));
    }

    [MySqlAisladoFact]
    public void StockInsuficiente_NoInsertaOrdenDetallesNiDescuenta()
    {
        Datos datos = Preparar(stockB: 2);
        var solicitud = Solicitud(datos);
        int detalles = ContarDetalles();
        ResultadoOrden resultado = Servicio().CrearOrden(solicitud);
        Assert.False(resultado.Exito);
        Assert.Contains($"Productos[{datos.B}]", resultado.Errores);
        Assert.Equal(0, ContarOrden(solicitud.Token));
        Assert.Equal(detalles, ContarDetalles());
        Assert.Equal(10, Stock(datos.A));
        Assert.Equal(2, Stock(datos.B));
    }

    [MySqlAisladoFact]
    public void ProductosRepetidos_SeAgrupanAntesDelStockYDeLosDetalles()
    {
        Datos datos = Preparar();
        var solicitud = Solicitud(datos, null, new LineaOrdenSolicitud(datos.B, 1), new LineaOrdenSolicitud(datos.A, 1), new LineaOrdenSolicitud(datos.B, 2), new LineaOrdenSolicitud(datos.A, 1));
        var resultado = Servicio().CrearOrden(solicitud);
        Assert.True(resultado.Exito);
        Assert.Equal(46.60m, resultado.Total);
        Assert.Equal(2, Convert.ToInt32(basePrueba.Escalar("SELECT COUNT(*) FROM DetalleOrdenes WHERE OrdenId = @Id;", ("@Id", resultado.OrdenId))));
        Assert.Equal(2, Convert.ToInt32(basePrueba.Escalar("SELECT Cantidad FROM DetalleOrdenes WHERE OrdenId = @Id AND ProductoId = @Producto;", ("@Id", resultado.OrdenId), ("@Producto", datos.A))));
        Assert.Equal(8, Stock(datos.A));
        Assert.Equal(7, Stock(datos.B));
    }

    [MySqlAisladoFact]
    public void FalloDespuesDeModificarStock_RevierteTodaLaCreacion()
    {
        Datos datos = Preparar();
        var solicitud = Solicitud(datos);
        int detalles = ContarDetalles();
        var fallida = new UnidadTrabajoInterceptada(Unidad, despues: (operacion, cuenta) =>
        {
            if (operacion == "Descontar" && cuenta == 2)
                throw new PersistenciaOrdenException("Fallo inyectado después del segundo descuento real.");
        });
        Assert.False(Servicio(unidad: fallida).CrearOrden(solicitud).Exito);
        Assert.Equal(0, ContarOrden(solicitud.Token));
        Assert.Equal(detalles, ContarDetalles());
        Assert.Equal(10, Stock(datos.A));
        Assert.Equal(10, Stock(datos.B));
        Assert.True(Servicio().CrearOrden(solicitud).Exito);
    }

    [MySqlAisladoFact]
    public void Anular_RestituyeUnaVezYConservaAuditoriaOriginal()
    {
        Datos datos = Preparar();
        var solicitud = Solicitud(datos);
        var creada = Servicio().CrearOrden(solicitud);
        object? fechaCreacion = basePrueba.Escalar("SELECT FechaCreacion FROM Ordenes WHERE Id = @Id;", ("@Id", creada.OrdenId));
        var administrador = new UsuarioPrueba { Rol = "Administrador", Username = "admin_prueba" };
        Assert.False(Servicio().AnularOrden(creada.OrdenId!.Value).Exito);
        Assert.Equal(8, Stock(datos.A));
        ResultadoOrden anulada = Servicio(administrador).AnularOrden(creada.OrdenId.Value);
        Assert.True(anulada.Exito);
        Assert.Equal(EstadoOrden.Anulada, anulada.Estado);
        Assert.Equal(10, Stock(datos.A));
        Assert.Equal(10, Stock(datos.B));
        Assert.Equal("recepcion_prueba", basePrueba.Escalar("SELECT CreadoPor FROM Ordenes WHERE Id = @Id;", ("@Id", creada.OrdenId)));
        Assert.Equal("admin_prueba", basePrueba.Escalar("SELECT AnuladoPor FROM Ordenes WHERE Id = @Id;", ("@Id", creada.OrdenId)));
        Assert.Equal(fechaCreacion, basePrueba.Escalar("SELECT FechaCreacion FROM Ordenes WHERE Id = @Id;", ("@Id", creada.OrdenId)));
        Assert.NotNull(basePrueba.Escalar("SELECT FechaAnulacion FROM Ordenes WHERE Id = @Id;", ("@Id", creada.OrdenId)));
        Assert.False(Servicio(administrador).AnularOrden(creada.OrdenId.Value).Exito);
        Assert.Equal(10, Stock(datos.A));
        Assert.Equal(10, Stock(datos.B));
        var reenvio = Servicio().CrearOrden(solicitud);
        Assert.True(reenvio.EsReenvio);
        Assert.Equal(EstadoOrden.Anulada, reenvio.Estado);
        Assert.Equal(10, Stock(datos.A));
    }

    [MySqlAisladoFact]
    public void FalloDespuesDeRestituirStock_RevierteEstadoAuditoriaYStock()
    {
        Datos datos = Preparar();
        var creada = Servicio().CrearOrden(Solicitud(datos));
        var administrador = new UsuarioPrueba { Rol = "Administrador" };
        var fallida = new UnidadTrabajoInterceptada(Unidad, despues: (operacion, cuenta) =>
        {
            if (operacion == "Restituir" && cuenta == 2)
                throw new PersistenciaOrdenException("Fallo inyectado después de restituir el segundo producto.");
        });
        Assert.False(Servicio(administrador, fallida).AnularOrden(creada.OrdenId!.Value).Exito);
        Assert.Equal("Activa", basePrueba.Escalar("SELECT Estado FROM Ordenes WHERE Id = @Id;", ("@Id", creada.OrdenId)));
        Assert.Equal(DBNull.Value, basePrueba.Escalar("SELECT AnuladoPor FROM Ordenes WHERE Id = @Id;", ("@Id", creada.OrdenId)));
        Assert.Equal(8, Stock(datos.A));
        Assert.Equal(7, Stock(datos.B));
        Assert.True(Servicio(administrador).AnularOrden(creada.OrdenId.Value).Exito);
    }

    [MySqlAisladoFact]
    public async Task DosEnviosSimultaneosDelToken_DevuelvenUnaOrdenInclusoSinStockRestante()
    {
        Datos datos = Preparar(stockA: 2);
        var solicitud = Solicitud(datos, null, new LineaOrdenSolicitud(datos.A, 2));
        using var barrera = new Barrier(2);
        var sincronizada = new UnidadTrabajoInterceptada(Unidad, despues: (operacion, cuenta) =>
        {
            if (operacion == "ObtenerToken" && cuenta == 1) Assert.True(barrera.SignalAndWait(TimeSpan.FromSeconds(10)));
        });
        var resultados = await Task.WhenAll(Task.Run(() => Servicio(unidad: sincronizada).CrearOrden(solicitud)),
            Task.Run(() => Servicio(unidad: sincronizada).CrearOrden(solicitud)));
        Assert.All(resultados, resultado => Assert.True(resultado.Exito, string.Join("; ", resultado.Errores.Values)));
        Assert.Equal(resultados[0].OrdenId, resultados[1].OrdenId);
        Assert.Single(resultados, resultado => resultado.EsReenvio);
        Assert.Equal(1, ContarOrden(solicitud.Token));
        Assert.Equal(0, Stock(datos.A));
    }

    [MySqlAisladoFact]
    public async Task TokenSimultaneoConProductosDistintos_LaClaveUnicaImpideDuplicados()
    {
        Datos datos = Preparar();
        Guid token = Guid.NewGuid();
        var primera = Solicitud(datos, token, new LineaOrdenSolicitud(datos.A, 1));
        var segunda = Solicitud(datos, token, new LineaOrdenSolicitud(datos.B, 1));
        using var barrera = new Barrier(2);
        int lecturasIniciales = 0;
        var sincronizada = new UnidadTrabajoInterceptada(Unidad, despues: (operacion, cuenta) =>
        {
            if (operacion == "ObtenerToken" && cuenta == 1 && Interlocked.Increment(ref lecturasIniciales) <= 2)
                Assert.True(barrera.SignalAndWait(TimeSpan.FromSeconds(10)));
        });
        var resultados = await Task.WhenAll(Task.Run(() => Servicio(unidad: sincronizada).CrearOrden(primera)),
            Task.Run(() => Servicio(unidad: sincronizada).CrearOrden(segunda)));
        Assert.All(resultados, resultado => Assert.True(resultado.Exito, string.Join("; ", resultado.Errores.Values)));
        Assert.Equal(resultados[0].OrdenId, resultados[1].OrdenId);
        Assert.Equal(1, ContarOrden(token));
        Assert.Equal(19, Stock(datos.A) + Stock(datos.B));
    }

    [MySqlAisladoFact]
    public async Task OrdenesConcurrentes_ElStockNuncaSeVuelveNegativo()
    {
        Datos datos = Preparar(stockA: 3);
        using var barrera = new Barrier(2);
        var sincronizada = new UnidadTrabajoInterceptada(Unidad, despues: (operacion, cuenta) =>
        {
            if (operacion == "ObtenerToken" && cuenta == 1) Assert.True(barrera.SignalAndWait(TimeSpan.FromSeconds(10)));
        });
        var primera = Solicitud(datos, null, new LineaOrdenSolicitud(datos.A, 2));
        var segunda = Solicitud(datos, null, new LineaOrdenSolicitud(datos.A, 2));
        var resultados = await Task.WhenAll(Task.Run(() => Servicio(unidad: sincronizada).CrearOrden(primera)),
            Task.Run(() => Servicio(unidad: sincronizada).CrearOrden(segunda)));
        Assert.Single(resultados, resultado => resultado.Exito);
        Assert.Equal(1, Stock(datos.A));
        Assert.Equal(1, ContarOrden(primera.Token) + ContarOrden(segunda.Token));
    }

    [MySqlAisladoFact]
    public async Task DosAnulacionesConcurrentes_RestituyenUnaSolaVez()
    {
        Datos datos = Preparar();
        var creada = Servicio().CrearOrden(Solicitud(datos));
        using var barrera = new Barrier(2);
        var sincronizada = new UnidadTrabajoInterceptada(Unidad, antes: (operacion, cuenta) =>
        {
            if (operacion == "ObtenerAnular" && cuenta == 1) Assert.True(barrera.SignalAndWait(TimeSpan.FromSeconds(10)));
        });
        var administrador = new UsuarioPrueba { Rol = "Administrador" };
        var resultados = await Task.WhenAll(Task.Run(() => Servicio(administrador, sincronizada).AnularOrden(creada.OrdenId!.Value)),
            Task.Run(() => Servicio(administrador, sincronizada).AnularOrden(creada.OrdenId!.Value)));
        Assert.Single(resultados, resultado => resultado.Exito);
        Assert.Contains("ya está anulada", resultados.Single(resultado => !resultado.Exito).Errores["OrdenId"]);
        Assert.Equal(10, Stock(datos.A));
        Assert.Equal(10, Stock(datos.B));
    }

    [MySqlAisladoFact]
    public void PreciosManipuladosEnJson_NoCambianPreciosNiTotalDeLaBase()
    {
        Datos datos = Preparar();
        string json = JsonSerializer.Serialize(new
        {
            VehiculoId = datos.Vehiculo, MecanicoId = datos.Mecanico, Fecha = DateTime.Today,
            Token = Guid.NewGuid(), Total = 0.01m,
            Lineas = new[] { new { ProductoId = datos.A, Cantidad = 2, PrecioUnitario = 0.01m, Subtotal = 0.02m } }
        });
        var solicitud = JsonSerializer.Deserialize<CrearOrdenSolicitud>(json)!;
        var resultado = Servicio().CrearOrden(solicitud);
        Assert.True(resultado.Exito);
        Assert.Equal(25m, resultado.Total);
        Assert.Equal(12.50m, Convert.ToDecimal(basePrueba.Escalar("SELECT PrecioUnitario FROM DetalleOrdenes WHERE OrdenId = @Id;", ("@Id", resultado.OrdenId))));
    }

    [MySqlAisladoFact]
    public void VehiculoSinClienteYReferenciasInexistentes_NoCreanOrden()
    {
        Datos datos = Preparar();
        basePrueba.Ejecutar("UPDATE Vehiculos SET ClienteId = NULL WHERE Id = @Id;", ("@Id", datos.Vehiculo));
        var solicitud = Solicitud(datos);
        Assert.Contains("VehiculoId", Servicio().CrearOrden(solicitud).Errores);
        Assert.Equal(0, ContarOrden(solicitud.Token));
        var inexistente = solicitud with { VehiculoId = int.MaxValue, MecanicoId = int.MaxValue };
        var errores = Servicio().CrearOrden(inexistente).Errores;
        Assert.Contains("VehiculoId", errores);
        Assert.Contains("MecanicoId", errores);
        Assert.Equal(10, Stock(datos.A));
    }

    [MySqlAisladoFact]
    public void ProductoInexistenteYTokenDeOtroUsuario_NoModificanStock()
    {
        Datos datos = Preparar();
        var solicitud = Solicitud(datos, null, new LineaOrdenSolicitud(int.MaxValue, 1));
        Assert.False(Servicio().CrearOrden(solicitud).Exito);
        Assert.Equal(0, ContarOrden(solicitud.Token));
        solicitud = Solicitud(datos);
        var creada = Servicio().CrearOrden(solicitud);
        Assert.True(creada.Exito);
        var otro = Servicio(new UsuarioPrueba { Username = "otro" }).CrearOrden(solicitud);
        Assert.False(otro.Exito);
        Assert.Contains("Token", otro.Errores);
        Assert.Equal(8, Stock(datos.A));
    }

    [MySqlAisladoFact]
    public void EntidadesReferenciadas_NoSeEliminanYNadiePierdeSuHistorial()
    {
        Datos datos = Preparar();
        var solicitud = Solicitud(datos);
        var creada = Servicio().CrearOrden(solicitud);
        var vehiculos = new VehiculoService(new VehiculoRepository(basePrueba.Factory), new ClienteRepository(basePrueba.Factory), new ValidacionVehiculos());
        Assert.Contains("órdenes asociadas", vehiculos.Delete(datos.Vehiculo));
        var productos = new ProductoService(new ProductoRepository(basePrueba.Factory), new ValidacionProductos(), new UsuarioPrueba());
        Assert.Contains("órdenes asociadas", productos.Eliminar(datos.A));
        var mecanicos = new MecanicoService(new CreadorMecanico(basePrueba.Factory), new ValidacionMecanicos());
        Assert.Contains("órdenes asociadas", mecanicos.Eliminar(datos.Mecanico));
        Assert.True(Servicio(new UsuarioPrueba { Rol = "Administrador" }).AnularOrden(creada.OrdenId!.Value).Exito);
        Assert.Contains("órdenes asociadas", productos.Eliminar(datos.A));
        Assert.Equal(1, ContarOrden(solicitud.Token));
    }

    [MySqlAisladoFact]
    public void HistorialServicios_TriggerPermaneceYRegistraElCambioDeCosto()
    {
        object? antes = basePrueba.Escalar("SELECT ACTION_STATEMENT FROM INFORMATION_SCHEMA.TRIGGERS WHERE TRIGGER_SCHEMA = DATABASE() AND TRIGGER_NAME = 'TRG_Servicios_HistorialCosto';");
        int servicio = checked((int)basePrueba.Ejecutar("INSERT INTO Servicios (Nombre, Descripcion, Costo, TiempoEstimadoHoras) VALUES ('Prueba EC2', 'Prueba', 20, 1);"));
        new DatabaseInitializer(basePrueba.Factory).Initialize();
        basePrueba.Ejecutar("UPDATE Servicios SET Costo = 30 WHERE Id = @Id;", ("@Id", servicio));
        Assert.Equal(1, Convert.ToInt32(basePrueba.Escalar("SELECT COUNT(*) FROM HistorialCostoServicios WHERE ServicioId = @Id AND CostoAnterior = 20 AND CostoNuevo = 30;", ("@Id", servicio))));
        Assert.Equal(antes, basePrueba.Escalar("SELECT ACTION_STATEMENT FROM INFORMATION_SCHEMA.TRIGGERS WHERE TRIGGER_SCHEMA = DATABASE() AND TRIGGER_NAME = 'TRG_Servicios_HistorialCosto';"));
    }

    [MySqlAisladoFact]
    public void MigracionOrdenes_SePuedeRepetirSinEliminarDatos()
    {
        using var aislada = new MySqlAisladoFixture();
        aislada.Ejecutar("DROP TABLE DetalleOrdenes; DROP TABLE Ordenes;");
        string sql = File.ReadAllText(Path.Combine(MySqlAisladoFixture.RaizProyecto, "docs/migraciones/004_ordenes_transaccionales.sql"));
        aislada.Ejecutar(sql);
        aislada.Ejecutar(sql);
        new DatabaseInitializer(aislada.Factory).Initialize();
        Assert.Equal(2, Convert.ToInt32(aislada.Escalar("SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME IN ('Ordenes', 'DetalleOrdenes');")));
    }
}
