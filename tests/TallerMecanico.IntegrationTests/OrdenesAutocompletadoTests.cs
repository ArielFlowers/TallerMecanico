using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging.Abstractions;
using TallerMecanico.Aplicacion.CasosUso.Ordenes;
using TallerMecanico.Aplicacion.Fachadas;
using TallerMecanico.Aplicacion.Puertos;
using TallerMecanico.Aplicacion.Servicios;
using TallerMecanico.Dominio.Entidades;
using TallerMecanico.Infraestructura.Persistencia.Fabricas;
using PaginaOrdenes = TallerMecanico.Pages.Ordenes.IndexModel;

namespace TallerMecanico.IntegrationTests;

public sealed class OrdenesAutocompletadoTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("1")]
    public async Task SugerenciasCortasYLargas_NoConsultanElPuerto(string? termino)
    {
        var puerto = new ConsultaPrueba();
        var fachada = Fachada(puerto);
        Assert.Empty(await fachada.SugerirAsync(termino));
        Assert.Empty(await fachada.SugerirAsync(new string('a', 101)));
        Assert.Equal(0, puerto.ConsultasSugerencias);
    }

    [Theory]
    [InlineData("Administrador")]
    [InlineData("Recepcionista")]
    public async Task Fachada_PropagaFiltrosIdExactoCancelacionYPermiteBusquedaManualDeUnDigito(string rol)
    {
        var puerto = new ConsultaPrueba();
        var fachada = Fachada(puerto, new UsuarioPrueba { Rol = rol });
        var fecha = new DateOnly(2026, 10, 12);
        using var cancelacion = new CancellationTokenSource();
        await fachada.SugerirAsync("  15  ", EstadoOrden.Activa, fecha, cancelacion.Token);
        Assert.Equal("15", puerto.Termino);
        Assert.Equal(EstadoOrden.Activa, puerto.Estado);
        Assert.Equal(fecha, puerto.Fecha);
        Assert.Equal(cancelacion.Token, puerto.Cancelacion);
        fachada.Obtener(EstadoOrden.Anulada, fecha, " 1 ", 15);
        Assert.Equal("1", puerto.Termino);
        Assert.Equal(15, puerto.Id);
        Assert.Equal(EstadoOrden.Anulada, puerto.Estado);
    }

    [Theory]
    [InlineData(false, "Administrador")]
    [InlineData(true, "Invitado")]
    public async Task SugerenciasYBusqueda_NoConsultanSinAutorizacion(bool autenticado, string rol)
    {
        var puerto = new ConsultaPrueba();
        var fachada = Fachada(puerto, new UsuarioPrueba { EstaAutenticado = autenticado, Rol = rol });
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => fachada.SugerirAsync("15"));
        Assert.Throws<UnauthorizedAccessException>(() => fachada.Obtener(terminoBusqueda: "1"));
        Assert.Equal(0, puerto.ConsultasSugerencias);
        Assert.Equal(0, puerto.ConsultasListado);
    }

    [Fact]
    public async Task Pagina_ConservaFiltrosAlRefrescarYRechazaFiltrosInvalidosEnSugerencias()
    {
        var puerto = new ConsultaPrueba();
        var pagina = new PaginaOrdenes(Fachada(puerto), null!, NullLogger<PaginaOrdenes>.Instance)
        {
            PageContext = new PageContext { HttpContext = new DefaultHttpContext() },
            Estado = EstadoOrden.Activa, Fecha = new DateOnly(2026, 10, 12),
            TerminoBusqueda = "Orden #15", OrdenSeleccionadaId = 15
        };
        pagina.OnGet();
        pagina.OnGet();
        Assert.Equal(2, puerto.ConsultasListado);
        Assert.Equal(15, puerto.Id);
        Assert.Equal(pagina.Estado, puerto.Estado);
        Assert.Equal(pagina.Fecha, puerto.Fecha);
        Assert.IsType<JsonResult>(await pagina.OnGetSugerenciasAsync("15"));
        pagina.ModelState.AddModelError(nameof(pagina.Fecha), "Fecha inválida");
        Assert.IsType<BadRequestObjectResult>(await pagina.OnGetSugerenciasAsync("15"));
        Assert.Equal(1, puerto.ConsultasSugerencias);
    }

    [MySqlAisladoFact]
    public async Task MySql_IdPrefijoPlacaCompartidaSeleccionExactaYFiltrosCombinados()
    {
        using var aislada = new MySqlAisladoFixture();
        CrearDatos(aislada);
        var fachada = Fachada(aislada);
        Assert.Equal(new[] { 151, 150, 15 }, (await fachada.SugerirAsync("15")).Select(orden => orden.Id));
        Assert.Equal(new[] { 151, 150, 15, 1 }, fachada.Obtener(terminoBusqueda: "1").Select(orden => orden.Id));
        Assert.Equal(4, (await fachada.SugerirAsync("123")).Count);
        Assert.Equal(4, (await fachada.SugerirAsync("123abc")).Count);
        Assert.Empty(await fachada.SugerirAsync("23ABC"));
        Assert.Empty(fachada.Obtener(terminoBusqueda: "23ABC"));
        Assert.Empty(await fachada.SugerirAsync("Ana"));
        var sugerencia = Assert.Single(await fachada.SugerirAsync("150"));
        Assert.Equal("Orden #150", sugerencia.TextoPrincipal);
        Assert.Equal("123ABC · Ana María Arce Pérez", sugerencia.TextoSecundario);
        Assert.Equal(15, Assert.Single(fachada.Obtener(terminoBusqueda: "Orden #15", ordenSeleccionadaId: 15)).Id);
        var fecha = new DateOnly(2026, 10, 12);
        Assert.Equal(new[] { 15, 1 }, fachada.Obtener(EstadoOrden.Activa, fecha, "123").Select(orden => orden.Id));
        Assert.Equal(new[] { 15, 1 }, (await fachada.SugerirAsync("123", EstadoOrden.Activa, fecha)).Select(orden => orden.Id));
        Assert.Equal(150, Assert.Single(fachada.Obtener(EstadoOrden.Anulada, fecha, "15")).Id);
        Assert.Empty(fachada.Obtener(EstadoOrden.Anulada, fecha, ordenSeleccionadaId: 15));
        Assert.Empty(fachada.Obtener(fecha: new DateOnly(2026, 10, 13), ordenSeleccionadaId: 15));
        Assert.Empty(fachada.Obtener(ordenSeleccionadaId: int.MaxValue));
        Assert.Empty(fachada.Obtener(ordenSeleccionadaId: -1));
        Assert.Equal(4, fachada.Obtener().Count);
    }

    [MySqlAisladoFact]
    public async Task MySql_LimiteSqlOrdenEstableYEscapeSinLimitarListadoManual()
    {
        using var aislada = new MySqlAisladoFixture();
        CrearDatos(aislada);
        for (int id = 200; id < 212; id++) CopiarOrden(aislada, id, EstadoOrden.Activa, new DateTime(2026, 10, 12));
        var fachada = Fachada(aislada);
        var sugerencias = await new CreadorConsultaOrdenes(aislada.Factory).CrearRepositorio().SugerirAsync("123");
        Assert.Equal(10, sugerencias.Count);
        Assert.Equal(sugerencias.ToArray(), (await fachada.SugerirAsync("123")).ToArray());
        Assert.Equal(16, fachada.Obtener(terminoBusqueda: "123").Count);
        Assert.Equal(16, fachada.Obtener(terminoBusqueda: "123").Select(orden => orden.Id).Distinct().Count());
        foreach (string termino in new[] { "%%", "__", "12%", "12_", "!!", "' OR 1=1 --", "<script>" })
        {
            Assert.Empty(await fachada.SugerirAsync(termino));
            Assert.Empty(fachada.Obtener(terminoBusqueda: termino));
        }
        aislada.Ejecutar("UPDATE Vehiculos SET ClienteId = NULL;");
        Assert.All(await fachada.SugerirAsync("123"), orden => Assert.Equal("123ABC", orden.TextoSecundario));
    }

    [MySqlAisladoFact]
    public async Task MySql_LecturasConservanStockAuditoriaDetallesYTotalHistorico()
    {
        using var aislada = new MySqlAisladoFixture();
        CrearDatos(aislada);
        aislada.Ejecutar("UPDATE Productos SET Precio = 999.99;");
        var fachada = Fachada(aislada);
        string antes = Instantanea(aislada);
        Assert.All(fachada.Obtener(terminoBusqueda: "123"), orden => Assert.Equal(31m, orden.Total));
        await fachada.SugerirAsync("123");
        fachada.Obtener(EstadoOrden.Activa, new DateOnly(2026, 10, 12), ordenSeleccionadaId: 1);
        Assert.Equal(2, fachada.ObtenerDetalle(1)!.Productos.Count);
        Assert.Equal(antes, Instantanea(aislada));
    }

    internal static void CrearDatos(MySqlAisladoFixture aislada)
    {
        Assert.Equal(1, ConsultaOrdenesMySqlTests.CrearOrden(aislada, new DateTime(2026, 10, 12)));
        aislada.Ejecutar("UPDATE Vehiculos SET Placa = '123ABC';");
        CopiarOrden(aislada, 15, EstadoOrden.Activa, new DateTime(2026, 10, 12, 23, 59, 59));
        CopiarOrden(aislada, 150, EstadoOrden.Anulada, new DateTime(2026, 10, 12, 23, 59, 59));
        CopiarOrden(aislada, 151, EstadoOrden.Activa, new DateTime(2026, 10, 13));
    }

    internal static string Instantanea(MySqlAisladoFixture aislada) => JsonSerializer.Serialize(new[]
    {
        aislada.Escalar("SELECT GROUP_CONCAT(CONCAT_WS('|', Id, Estado, Total, Token, CreadoPor, FechaCreacion, AnuladoPor, FechaAnulacion) ORDER BY Id) FROM Ordenes;"),
        aislada.Escalar("SELECT GROUP_CONCAT(CONCAT_WS('|', Id, Stock, Precio) ORDER BY Id) FROM Productos;"),
        aislada.Escalar("SELECT GROUP_CONCAT(CONCAT_WS('|', Id, OrdenId, ProductoId, Cantidad, PrecioUnitario, Subtotal) ORDER BY Id) FROM DetalleOrdenes;")
    });

    internal static ConsultaOrdenesFacade Fachada(MySqlAisladoFixture aislada) =>
        SoporteComprobantePdf.CrearFachada(new ConsultaOrdenesService(
            new CreadorConsultaOrdenes(aislada.Factory).CrearRepositorio(), new UsuarioPrueba()));

    private static ConsultaOrdenesFacade Fachada(ConsultaPrueba puerto, UsuarioPrueba? usuario = null) =>
        SoporteComprobantePdf.CrearFachada(new ConsultaOrdenesService(puerto, usuario ?? new UsuarioPrueba()));

    private static void CopiarOrden(MySqlAisladoFixture aislada, int id, EstadoOrden estado, DateTime fecha) =>
        aislada.Ejecutar("""
            INSERT INTO Ordenes (Id, VehiculoId, MecanicoId, Fecha, Estado, Total, Token,
                CreadoPor, FechaCreacion, AnuladoPor, FechaAnulacion)
            SELECT @Id, VehiculoId, MecanicoId, @Fecha, @Estado, Total, @Token, CreadoPor, FechaCreacion,
                @AnuladoPor, @FechaAnulacion FROM Ordenes WHERE Id = 1;
            """, ("@Id", id), ("@Fecha", fecha), ("@Estado", estado.ToString()), ("@Token", Guid.NewGuid().ToString("D")),
            ("@AnuladoPor", estado == EstadoOrden.Anulada ? "admin_prueba" : null),
            ("@FechaAnulacion", estado == EstadoOrden.Anulada ? new DateTime(2026, 10, 14) : null));

    private sealed class ConsultaPrueba : IConsultaOrdenesPort
    {
        public string? Termino { get; private set; }
        public int? Id { get; private set; }
        public EstadoOrden? Estado { get; private set; }
        public DateOnly? Fecha { get; private set; }
        public CancellationToken Cancelacion { get; private set; }
        public int ConsultasSugerencias { get; private set; }
        public int ConsultasListado { get; private set; }
        public OrdenDetalle? ObtenerDetalle(int id) => null;
        public IReadOnlyList<OrdenResumen> Obtener(EstadoOrden? estado = null, DateOnly? fecha = null,
            string? terminoBusqueda = null, int? ordenSeleccionadaId = null)
        {
            ConsultasListado++; Termino = terminoBusqueda; Estado = estado; Fecha = fecha; Id = ordenSeleccionadaId;
            return [];
        }
        public Task<IReadOnlyList<SugerenciaOrden>> SugerirAsync(string termino, EstadoOrden? estado = null,
            DateOnly? fecha = null, CancellationToken cancellationToken = default)
        {
            ConsultasSugerencias++; Termino = termino; Estado = estado; Fecha = fecha; Cancelacion = cancellationToken;
            return Task.FromResult<IReadOnlyList<SugerenciaOrden>>([]);
        }
    }
}
