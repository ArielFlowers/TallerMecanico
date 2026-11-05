using TallerMecanico.Aplicacion.DTOs.Reportes;
using TallerMecanico.Infraestructura.Persistencia.MySql;
using Xunit;

namespace TallerMecanico.IntegrationTests.Reportes;

[Collection("MySQL aislado")]
[Trait("Area", "Datos")]
public sealed class ReportesMySqlTests(MySqlAisladoFixture basePrueba)
{
    private static int _ci = 7000000;

    private sealed record OrdenCreada(int OrdenId, int MecanicoId);

    private int CrearClienteYVehiculo(out int vehiculoId)
    {
        int ci = Interlocked.Increment(ref _ci);
        int clienteId = checked((int)basePrueba.Ejecutar("""
            INSERT INTO Clientes (Ci, Nombres, PrimerApellido, SegundoApellido, Celular)
            VALUES (@Ci, 'Cliente', 'Reporte', 'Prueba', '71234567');
            """, ("@Ci", ci.ToString())));

        vehiculoId = checked((int)basePrueba.Ejecutar("""
            INSERT INTO Vehiculos (Placa, Marca, Modelo, Kilometraje, ClienteId, EsPlacaExtranjera)
            VALUES (@Placa, 'Toyota', 'Corolla', 100, @Cliente, TRUE);
            """, ("@Placa", Guid.NewGuid().ToString("N")[..15].ToUpperInvariant()),
            ("@Cliente", clienteId)));

        return clienteId;
    }

    private int CrearMecanico()
    {
        int ci = Interlocked.Increment(ref _ci);
        return checked((int)basePrueba.Ejecutar("""
            INSERT INTO Mecanicos (Ci, Nombres, PrimerApellido, SegundoApellido,
                Genero, Especialidad, Celular)
            VALUES (@Ci, 'Mecanico', 'Reporte', 'Prueba',
                'Masculino', 'Sin Especialidad', '71234567');
            """, ("@Ci", ci.ToString())));
    }

    private int CrearServicio(string nombre, decimal costo)
    {
        string codigo = $"REP_{Guid.NewGuid():N}";
        return checked((int)basePrueba.Ejecutar("""
            INSERT INTO Servicios (CodigoCatalogo, Nombre, Descripcion, Costo, TiempoEstimadoHoras)
            VALUES (@Codigo, @Nombre, 'Servicio de prueba', @Costo, 1.5);
            """, ("@Codigo", codigo), ("@Nombre", nombre), ("@Costo", costo)));
    }

    private OrdenCreada CrearOrden(
        int vehiculoId,
        int mecanicoId,
        int servicioId,
        decimal costoServicio,
        decimal total,
        DateTime fecha,
        bool anulada)
    {
        int ordenId = checked((int)basePrueba.Ejecutar("""
            INSERT INTO Ordenes (VehiculoId, MecanicoId, ServicioId, CostoServicio,
                Fecha, Estado, Total, Token, CreadoPor, FechaCreacion,
                AnuladoPor, FechaAnulacion)
            VALUES (@Vehiculo, @Mecanico, @Servicio, @Costo,
                @Fecha, @Estado, @Total, @Token, 'recepcion_prueba', UTC_TIMESTAMP(),
                @AnuladoPor, @FechaAnulacion);
            """, ("@Vehiculo", vehiculoId), ("@Mecanico", mecanicoId),
            ("@Servicio", servicioId), ("@Costo", costoServicio),
            ("@Fecha", fecha), ("@Estado", anulada ? "Anulada" : "Activa"),
            ("@Total", total), ("@Token", Guid.NewGuid().ToString("D")),
            ("@AnuladoPor", anulada ? "admin_prueba" : null),
            ("@FechaAnulacion", anulada ? fecha.AddDays(1) : null)));

        return new OrdenCreada(ordenId, mecanicoId);
    }

    private void AgregarDetalleServicio(
        int ordenId,
        int servicioId,
        string nombre,
        decimal costo)
    {
        basePrueba.Ejecutar("""
            INSERT INTO DetalleServiciosOrdenes
                (OrdenId, ServicioId, NombreServicio, DescripcionServicio,
                 Costo, TiempoEstimadoHoras)
            VALUES (@Orden, @Servicio, @Nombre, 'Detalle de prueba', @Costo, 1.5);
            """, ("@Orden", ordenId), ("@Servicio", servicioId),
            ("@Nombre", nombre), ("@Costo", costo));
    }

    [MySqlAisladoFact]
    public void ReporteOrdenes_IncluyeTodosLosServiciosYNoSumaAnuladas()
    {
        int vehiculoId = 0;
        CrearClienteYVehiculo(out vehiculoId);
        int mecanicoId = CrearMecanico();

        int servicioA = CrearServicio("Servicio A", 100m);
        int servicioB = CrearServicio("Servicio B", 50m);
        int servicioC = CrearServicio("Servicio C", 900m);

        var fecha = new DateTime(2031, 1, 15, 10, 0, 0);

        OrdenCreada activa = CrearOrden(
            vehiculoId, mecanicoId, servicioA, 100m, 150m, fecha, anulada: false);
        AgregarDetalleServicio(activa.OrdenId, servicioA, "Servicio A", 100m);
        AgregarDetalleServicio(activa.OrdenId, servicioB, "Servicio B", 50m);

        OrdenCreada anulada = CrearOrden(
            vehiculoId, mecanicoId, servicioC, 900m, 999m, fecha, anulada: true);
        AgregarDetalleServicio(anulada.OrdenId, servicioC, "Servicio C", 900m);

        var adapter = new ConsultaReportesMySqlAdapter(basePrueba.Factory);
        var documento = adapter.ObtenerReporteOrdenes(
            new FiltroReporteOrdenes
            {
                FechaDesde = DateOnly.FromDateTime(fecha),
                FechaHasta = DateOnly.FromDateTime(fecha)
            },
            "admin");

        Assert.Equal(2, documento.Resumen.TotalOrdenes);
        Assert.Equal(1, documento.Resumen.OrdenesActivas);
        Assert.Equal(1, documento.Resumen.OrdenesAnuladas);
        Assert.Equal(150m, documento.Resumen.TotalGeneral);

        FilaReporteOrden filaActiva = Assert.Single(
            documento.Filas, f => f.OrdenId == activa.OrdenId);
        Assert.Equal("Servicio A, Servicio B", filaActiva.NombreServicio);
        Assert.Equal(150m, filaActiva.Total);

        FilaReporteOrden filaAnulada = Assert.Single(
            documento.Filas, f => f.OrdenId == anulada.OrdenId);
        Assert.Equal("Servicio C", filaAnulada.NombreServicio);
        Assert.Equal("Anulada", filaAnulada.Estado);
    }

    [MySqlAisladoFact]
    public void ReporteMecanicos_SoloCuentaOrdenesActivas()
    {
        int vehiculoId = 0;
        CrearClienteYVehiculo(out vehiculoId);
        int mecanicoConOrdenes = CrearMecanico();
        int mecanicoSinOrdenes = CrearMecanico();
        int servicio = CrearServicio("Servicio mecanico", 200m);

        var fecha = new DateTime(2031, 2, 10, 9, 0, 0);

        CrearOrden(vehiculoId, mecanicoConOrdenes, servicio, 200m, 200m,
            fecha, anulada: false);
        CrearOrden(vehiculoId, mecanicoConOrdenes, servicio, 500m, 500m,
            fecha, anulada: true);

        var adapter = new ConsultaReportesMySqlAdapter(basePrueba.Factory);

        DocumentoReporteMecanicos conOrdenes = adapter.ObtenerReporteMecanicos(
            new FiltroReporteMecanicos { MecanicoId = mecanicoConOrdenes },
            "admin");

        FilaReporteMecanico fila = Assert.Single(conOrdenes.Filas);
        Assert.Equal(1, fila.TotalOrdenes);
        Assert.Equal(200m, fila.TotalFacturado);
        Assert.Equal(200m, fila.PromedioPorOrden);
        Assert.Equal(1, conOrdenes.Resumen.TotalOrdenes);
        Assert.Equal(200m, conOrdenes.Resumen.TotalFacturado);

        DocumentoReporteMecanicos sinOrdenes = adapter.ObtenerReporteMecanicos(
            new FiltroReporteMecanicos { MecanicoId = mecanicoSinOrdenes },
            "admin");

        FilaReporteMecanico vacia = Assert.Single(sinOrdenes.Filas);
        Assert.Equal(0, vacia.TotalOrdenes);
        Assert.Equal(0m, vacia.TotalFacturado);
        Assert.Equal(0m, vacia.PromedioPorOrden);
    }
}
