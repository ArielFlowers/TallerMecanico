using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging.Abstractions;
using MySqlConnector;
using TallerMecanico.Aplicacion.CasosUso.Ordenes;
using TallerMecanico.Aplicacion.Servicios;
using TallerMecanico.Aplicacion.Validaciones;
using TallerMecanico.Dominio.Entidades;
using TallerMecanico.Infraestructura.Persistencia;
using TallerMecanico.Infraestructura.Persistencia.MySql;

namespace TallerMecanico.IntegrationTests;

public sealed class CatalogoServiciosMySqlTests
{
    [MySqlAisladoFact]
    public void DatosFaltantesYSimilares_SeInformanSinInventarNiEscribir()
    {
        using var aislada = new MySqlAisladoFixture();
        aislada.Ejecutar("INSERT INTO Servicios (Nombre,Descripcion,Costo,TiempoEstimadoHoras) VALUES ('Cambio de aceite sintético','Descripción anterior',55,2);");
        var carga = new InicializadorCatalogoServicios(aislada.Factory);
        string antes = JsonSerializer.Serialize(new ServicioRepository(aislada.Factory).GetAll());

        Assert.Contains(carga.Analizar(), linea => linea.Contains("1: Cambio de aceite sintético"));
        Assert.Equal(10, carga.Incorporar().Count);
        Assert.Equal(antes, JsonSerializer.Serialize(new ServicioRepository(aislada.Factory).GetAll()));
        Assert.Empty(new ServicioRepository(aislada.Factory).ObtenerCatalogo());
    }

    [MySqlAisladoFact]
    public void DiezServicios_ConservanIdentidadPreciosInicialesYCuatroUnidadesEditables()
    {
        using var aislada = new MySqlAisladoFixture();
        Cargar(aislada);
        var repositorio = new ServicioRepository(aislada.Factory);
        var esperados = new (string Nombre, decimal Costo, string? Unidad)[]
        {
            ("Cambio de aceite",250m,null), ("Cambio de filtros",100m,"unidad"),
            ("Cambio de pastillas de freno",250m,"unidad"), ("Diagnóstico con escáner",120m,null),
            ("Reparación de motor",1500m,null), ("Cambio de bujías",180m,"unidad"),
            ("Reparación de suspensión",400m,"unidad"), ("Cambio de batería",550m,null),
            ("Cambio de embrague",1000m,null), ("Alineación y balanceo",180m,null)
        };
        Assert.Equal(10, repositorio.ObtenerCatalogo().Count);
        foreach (var (nombre, costo, unidad) in esperados)
        {
            Servicio servicio = Assert.Single(repositorio.ObtenerCatalogo(), servicio => servicio.Nombre == nombre);
            Assert.Equal(costo, servicio.Costo);
            Assert.Equal(unidad, servicio.UnidadCantidad);
            Assert.NotNull(servicio.CodigoCatalogo);
        }
        Assert.Equal(4, repositorio.ObtenerCatalogo().Count(servicio => servicio.UnidadCantidad == "unidad"));
    }

    [MySqlAisladoFact]
    public async Task NuevaOrden_SoloConsultaCatalogoYAdministracionConservaOtrosServicios()
    {
        using var aislada = new MySqlAisladoFixture();
        NuevaOrdenAutocompletadoTests.Preparar(aislada);
        Cargar(aislada);
        int antiguo = checked((int)aislada.Ejecutar("INSERT INTO Servicios (Nombre,Descripcion,Costo,TiempoEstimadoHoras) VALUES ('Cambio de aceite','Servicio antiguo duplicado',25,1);"));
        var pagina = NuevaOrdenAutocompletadoTests.CrearPagina(aislada);
        var todos = JsonSerializer.SerializeToElement((await pagina.OnGetSugerenciasServiciosAsync(null)).Value);
        Assert.Equal(10, todos.GetArrayLength());
        var sugerencias = JsonSerializer.SerializeToElement((await pagina.OnGetSugerenciasServiciosAsync("cambio")).Value);
        Assert.Equal(6, sugerencias.GetArrayLength());
        Assert.IsType<NotFoundResult>(pagina.OnGetServicioSeleccionado(antiguo));
        Assert.IsType<JsonResult>(pagina.OnGetServicioSeleccionado(1));
        Assert.Equal(11, ServiciosAutocompletadoTests.CrearServicio(aislada).ObtenerTodos().Count);
        pagina.OnGet();
        pagina.Formulario.VehiculoId = 1;
        pagina.Formulario.MecanicoId = 1;
        pagina.Formulario.ServicioIds = [antiguo];
        Assert.IsType<PageResult>(pagina.OnPostGuardar());
        Assert.Contains(pagina.ModelState.Values.SelectMany(entrada => entrada.Errors), error => error.ErrorMessage.Contains("catálogo"));
        Assert.Equal(0, Convert.ToInt32(aislada.Escalar("SELECT COUNT(*) FROM Ordenes;")));
    }

    [MySqlAisladoFact]
    public void Cantidades_SeValidanConMySqlParaLosDiezServiciosSinDuplicarLineas()
    {
        using var aislada = new MySqlAisladoFixture();
        NuevaOrdenAutocompletadoTests.Preparar(aislada);
        Cargar(aislada);
        foreach (Servicio servicio in new ServicioRepository(aislada.Factory).ObtenerCatalogo())
        {
            Assert.True(Ordenes(aislada).CrearOrden(Solicitud(servicio.Id, 1)).Exito);
            var cantidadTres = Ordenes(aislada).CrearOrden(Solicitud(servicio.Id, 3));
            if (servicio.UnidadCantidad is null)
                Assert.Contains($"Servicios[{servicio.Id}]", cantidadTres.Errores);
            else
            {
                Assert.True(cantidadTres.Exito);
                Assert.Equal(servicio.Costo * 3, cantidadTres.Total);
            }
            Assert.False(Ordenes(aislada).CrearOrden(Solicitud(servicio.Id, 1) with { ServicioIds = [servicio.Id, servicio.Id] }).Exito);
        }
        Assert.Equal(14, Convert.ToInt32(aislada.Escalar("SELECT COUNT(*) FROM Ordenes;")));
        Assert.Equal(20, Convert.ToInt32(aislada.Escalar("SELECT SUM(Stock) FROM Productos;")));
        Assert.Equal(0, Convert.ToInt32(aislada.Escalar("SELECT COUNT(*) FROM DetalleOrdenes;")));
    }

    [MySqlAisladoFact]
    public void IdentidadDuplicadaYUnidadManipulada_NoPermitenEludirLaReglaDeAceite()
    {
        using var aislada = new MySqlAisladoFixture();
        NuevaOrdenAutocompletadoTests.Preparar(aislada);
        Cargar(aislada);
        Assert.Throws<MySqlException>(() => aislada.Ejecutar("INSERT INTO Servicios (CodigoCatalogo,Nombre,Descripcion,Costo,TiempoEstimadoHoras) VALUES ('CAMBIO_ACEITE','Otro aceite','Duplicado',1,1);"));
        var administracion = ServiciosAutocompletadoTests.CrearServicio(aislada);
        Servicio aceite = administracion.ObtenerPorId(1)!;
        aceite.UnidadCantidad = "unidad";
        Assert.Throws<InvalidOperationException>(() => administracion.Actualizar(aceite));
        Assert.Throws<InvalidOperationException>(() => administracion.Eliminar(1));
        aislada.Ejecutar("UPDATE Servicios SET UnidadCantidad = 'unidad' WHERE Id = 1;");
        Assert.Null(administracion.ObtenerDelCatalogo(1));
        Assert.False(Ordenes(aislada).CrearOrden(Solicitud(1, 3)).Exito);
        Assert.Equal(0, Convert.ToInt32(aislada.Escalar("SELECT COUNT(*) FROM Ordenes;")));
    }

    [MySqlAisladoFact]
    public void Incorporacion_ReutilizaIdDescripcionTiempoYNoReiniciaPreciosAutorizados()
    {
        using var aislada = new MySqlAisladoFixture();
        NuevaOrdenAutocompletadoTests.Preparar(aislada);
        aislada.Ejecutar("UPDATE Servicios SET CodigoCatalogo = NULL WHERE Id = 1;");
        var carga = new InicializadorCatalogoServicios(aislada.Factory);
        Cargar(aislada);
        var administracion = ServiciosAutocompletadoTests.CrearServicio(aislada);
        Servicio aceite = administracion.ObtenerPorId(1)!;
        Assert.Equal("CAMBIO_ACEITE", aceite.CodigoCatalogo);
        Assert.Equal("Lubricación del motor", aceite.Descripcion);
        Assert.Equal(1m, aceite.TiempoEstimadoHoras);
        Assert.Equal(250m, aceite.Costo);
        var solicitud = Solicitud(1, 1);
        var creada = Ordenes(aislada).CrearOrden(solicitud);
        Assert.True(creada.Exito);
        aceite.Costo = 275;
        aceite.CodigoCatalogo = "CAMBIO_FILTROS";
        administracion.Actualizar(aceite);
        Assert.Empty(carga.Incorporar());
        Assert.Empty(carga.Incorporar());
        Assert.Equal(10, administracion.ObtenerCatalogo().Count);
        Assert.Equal(275m, administracion.ObtenerPorId(1)!.Costo);
        Assert.Equal("CAMBIO_ACEITE", administracion.ObtenerPorId(1)!.CodigoCatalogo);
        Assert.Equal(2, Convert.ToInt32(aislada.Escalar("SELECT COUNT(*) FROM HistorialCostoServicios WHERE ServicioId = 1;")));
        Assert.Equal(250m, new ConsultaOrdenesRepository(aislada.Factory).ObtenerDetalle(creada.OrdenId!.Value)!.Total);
        Assert.Equal(250m, Convert.ToDecimal(aislada.Escalar("SELECT Costo FROM DetalleServiciosOrdenes;")));
        var reenvio = Ordenes(aislada).CrearOrden(solicitud);
        Assert.True(reenvio.EsReenvio);
        Assert.Equal(250m, reenvio.Total);
        Assert.Equal(275m, Ordenes(aislada).CrearOrden(Solicitud(1, 1)).Total);
    }

    [MySqlAisladoFact]
    public void CoincidenciasAmbiguas_NoSeFusionanYRequierenConfirmarId()
    {
        using var aislada = new MySqlAisladoFixture();
        aislada.Ejecutar("INSERT INTO Servicios (Nombre,Descripcion,Costo,TiempoEstimadoHoras) VALUES ('Cambio de aceite','Primero',25,1),('CAMBIO DE ACEITE','Segundo',30,2);");
        var carga = new InicializadorCatalogoServicios(aislada.Factory);
        Assert.Contains(carga.Incorporar(DatosConfirmados()), pendiente => pendiente.Contains("ambigua"));
        Assert.Equal(2, Convert.ToInt32(aislada.Escalar("SELECT COUNT(*) FROM Servicios;")));
        var confirmados = DatosConfirmados();
        confirmados[0].Id = 2;
        Assert.Empty(carga.Incorporar(confirmados));
        Assert.Equal(11, Convert.ToInt32(aislada.Escalar("SELECT COUNT(*) FROM Servicios;")));
        Assert.Equal(2, new ServicioRepository(aislada.Factory).ObtenerCatalogo().Single(servicio => servicio.CodigoCatalogo == "CAMBIO_ACEITE").Id);
        Assert.Null(new ServicioRepository(aislada.Factory).GetById(1)!.CodigoCatalogo);
    }

    [MySqlAisladoFact]
    public void ServicioHistoricoFueraDelCatalogo_SeConservaConSusRelacionesEImportes()
    {
        using var aislada = new MySqlAisladoFixture();
        NuevaOrdenAutocompletadoTests.Preparar(aislada);
        aislada.Ejecutar("UPDATE Servicios SET CodigoCatalogo = NULL, Nombre = 'Servicio antiguo' WHERE Id = 1;");
        int orden = checked((int)aislada.Ejecutar("""
            INSERT INTO Ordenes (VehiculoId,MecanicoId,ServicioId,CostoServicio,Fecha,Total,Token,CreadoPor,FechaCreacion)
            VALUES (1,1,1,25,CURRENT_DATE,25,@Token,'historico',UTC_TIMESTAMP());
            """, ("@Token", Guid.NewGuid().ToString("D"))));
        new DatabaseInitializer(aislada.Factory).Initialize();
        var consulta = new ConsultaOrdenesRepository(aislada.Factory);
        string antes = JsonSerializer.Serialize(consulta.ObtenerDetalle(orden));
        Assert.Contains(new InicializadorCatalogoServicios(aislada.Factory).Analizar(), linea => linea.Contains("Órdenes=1"));
        Cargar(aislada);
        Assert.Equal(antes, JsonSerializer.Serialize(consulta.ObtenerDetalle(orden)));
        Assert.Equal(1, Convert.ToInt32(aislada.Escalar("SELECT ServicioId FROM Ordenes;")));
        Assert.Equal(11, new ServicioRepository(aislada.Factory).GetAll().Count);
        Assert.Null(new ServicioRepository(aislada.Factory).ObtenerDelCatalogo(1));
        Assert.False(Ordenes(aislada).CrearOrden(Solicitud(1, 1)).Exito);
    }

    [MySqlAisladoFact]
    public void AdoptarServicioConOrdenAnterior_NoCambiaRelacionNiPrecioHistorico()
    {
        using var aislada = new MySqlAisladoFixture();
        NuevaOrdenAutocompletadoTests.Preparar(aislada);
        aislada.Ejecutar("UPDATE Servicios SET CodigoCatalogo = NULL WHERE Id = 1;");
        int orden = checked((int)aislada.Ejecutar("""
            INSERT INTO Ordenes (VehiculoId,MecanicoId,ServicioId,CostoServicio,Fecha,Total,Token,CreadoPor,FechaCreacion)
            VALUES (1,1,1,25,CURRENT_DATE,25,@Token,'historico',UTC_TIMESTAMP());
            """, ("@Token", Guid.NewGuid().ToString("D"))));
        new DatabaseInitializer(aislada.Factory).Initialize();
        var consulta = new ConsultaOrdenesRepository(aislada.Factory);
        string antes = JsonSerializer.Serialize(consulta.ObtenerDetalle(orden));

        Cargar(aislada);

        Assert.Equal(antes, JsonSerializer.Serialize(consulta.ObtenerDetalle(orden)));
        Assert.Equal(1, Convert.ToInt32(aislada.Escalar("SELECT ServicioId FROM Ordenes;")));
        Assert.Equal(25m, Convert.ToDecimal(aislada.Escalar("SELECT CostoServicio FROM Ordenes;")));
        Assert.Equal(25m, Convert.ToDecimal(aislada.Escalar("SELECT Costo FROM DetalleServiciosOrdenes;")));
        Assert.Equal(250m, new ServicioRepository(aislada.Factory).ObtenerDelCatalogo(1)!.Costo);
        Assert.Equal(1, Convert.ToInt32(aislada.Escalar("SELECT COUNT(*) FROM HistorialCostoServicios WHERE ServicioId = 1;")));
    }

    [MySqlAisladoFact]
    public void MigracionAditiva_SeRepiteSinDuplicarIndicesNiModificarServicios()
    {
        using var aislada = new MySqlAisladoFixture();
        aislada.Ejecutar("ALTER TABLE Servicios DROP INDEX UQ_Servicios_CodigoCatalogo, DROP COLUMN CodigoCatalogo;");
        aislada.Ejecutar("INSERT INTO Servicios (Nombre,Descripcion,Costo,TiempoEstimadoHoras) VALUES ('Anterior','Sin cambios',30,2);");
        string sql = File.ReadAllText(Path.Combine(MySqlAisladoFixture.RaizProyecto, "docs/migraciones/008_catalogo_servicios.sql"));
        aislada.Ejecutar(sql);
        aislada.Ejecutar(sql);
        new DatabaseInitializer(aislada.Factory).Initialize();
        new DatabaseInitializer(aislada.Factory).Initialize();
        Assert.Equal(1, Convert.ToInt32(aislada.Escalar("SELECT COUNT(*) FROM Servicios;")));
        Assert.Equal(DBNull.Value, aislada.Escalar("SELECT CodigoCatalogo FROM Servicios;"));
        Assert.Equal(1, Convert.ToInt32(aislada.Escalar("SELECT COUNT(*) FROM information_schema.statistics WHERE table_schema=DATABASE() AND table_name='Servicios' AND column_name='CodigoCatalogo' AND non_unique=0;")));
    }

    internal static List<Servicio> DatosConfirmados() => CatalogoServicios.Servicios.Select(definicion => new Servicio
    {
        CodigoCatalogo = definicion.Codigo, Descripcion = "Descripción aislada de prueba", TiempoEstimadoHoras = 1.25m
    }).ToList();

    internal static void Cargar(MySqlAisladoFixture aislada) =>
        Assert.Empty(new InicializadorCatalogoServicios(aislada.Factory).Incorporar(DatosConfirmados()));

    private static CrearOrdenSolicitud Solicitud(int servicioId, int cantidad) => new(
        1, 1, DateTime.Today, Guid.NewGuid(), [], ServicioIds: [servicioId], CantidadesServicios: [new(servicioId, cantidad)]);

    private static OrdenServicioService Ordenes(MySqlAisladoFixture aislada) => new(
        new MySqlUnidadTrabajoOrden(aislada.Factory, NullLogger<MySqlUnidadTrabajoOrden>.Instance),
        new UsuarioPrueba(), new ValidacionOrdenes(), TimeProvider.System, NullLogger<OrdenServicioService>.Instance);
}
