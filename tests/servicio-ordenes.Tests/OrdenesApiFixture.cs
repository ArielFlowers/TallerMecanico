using System.Data.Common;
using System.Net;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TallerMecanico.Aplicacion.CasosUso.Ordenes;
using TallerMecanico.Aplicacion.CasosUso.Ordenes.Contratos;
using TallerMecanico.Aplicacion.Fachadas;
using TallerMecanico.Aplicacion.Puertos;
using TallerMecanico.Aplicacion.Validaciones;
using TallerMecanico.Controllers;
using TallerMecanico.Dominio.Entidades;
using TallerMecanico.Dominio.Servicios;
using TallerMecanico.Infraestructura.Seguridad;

namespace TallerMecanico.IntegrationTests;

public sealed class OrdenesApiFixture : IAsyncLifetime
{
    private WebApplication? _app;
    internal PuertosOrdenesApi Puertos { get; } = new();
    public HttpClient Http { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = "Testing",
            ContentRootPath = AppContext.BaseDirectory,
            ApplicationName = typeof(OrdenesController).Assembly.GetName().Name
        });
        builder.Configuration.Sources.Clear();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        { ["ClaveInterna"] = ServiciosEnPrueba.ClaveInterna });
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(opciones => opciones.Listen(IPAddress.Loopback, 0));
        builder.Services.AddControllers(opciones =>
        {
            opciones.Filters.Add<ErroresApiFilter>();
            opciones.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true;
        }).AddApplicationPart(typeof(OrdenesController).Assembly);
        builder.Services.AddAuthentication(ClaveInternaHandler.Esquema)
            .AddScheme<AuthenticationSchemeOptions, ClaveInternaHandler>(ClaveInternaHandler.Esquema, null);
        builder.Services.AddAuthorization(opciones =>
            opciones.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddScoped<ICurrentUser, CurrentUser>();
        builder.Services.AddSingleton(Puertos);
        builder.Services.AddSingleton<IOrdenQueryRepository>(Puertos);
        builder.Services.AddSingleton<IUnidadTrabajoOrden>(Puertos);
        builder.Services.AddSingleton<IComprobantePdf, ComprobantePdfPrueba>();
        builder.Services.AddSingleton<ImporteEnPalabras>();
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddScoped<ValidacionOrdenes>();
        builder.Services.AddScoped<ConsultarOrdenesCasoUso>();
        builder.Services.AddScoped<CrearOrdenCasoUso>();
        builder.Services.AddScoped<AnularOrdenCasoUso>();
        builder.Services.AddScoped<GenerarComprobanteCasoUso>();
        builder.Services.AddScoped<ConsultaOrdenesFacade>();
        builder.Services.AddScoped<OrdenServicioFacade>();

        // MVC, filtros, autenticación y Aplicación reales; la persistencia es un doble en memoria.
        _app = builder.Build();
        _app.UseAuthentication();
        _app.UseAuthorization();
        _app.MapControllers();
        try
        {
            await _app.StartAsync();
            string direccion = _app.Services.GetRequiredService<IServer>()
                .Features.Get<IServerAddressesFeature>()!.Addresses.Single();
            Http = new HttpClient { BaseAddress = new Uri(direccion), Timeout = TimeSpan.FromSeconds(10) };
        }
        catch
        {
            await _app.DisposeAsync();
            throw;
        }
    }

    public async Task DisposeAsync()
    {
        Http?.Dispose();
        if (_app is null) return;
        using var cancelacion = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        try { await _app.StopAsync(cancelacion.Token); }
        finally { await _app.DisposeAsync(); }
    }
}

internal sealed class PuertosOrdenesApi : IOrdenQueryRepository, IUnidadTrabajoOrden
{
    public List<Orden> Ordenes { get; } = [];
    public List<DetalleOrden> Detalles { get; } = [];
    public List<DetalleServicioOrden> ServiciosOrden { get; } = [];
    public List<Producto> Productos { get; } = [];
    public List<Servicio> Servicios { get; } = [];
    public List<OrdenResumen> Resumenes { get; } = [];
    public Dictionary<int, OrdenDetalle> DetallesConsulta { get; } = [];
    public int ConsultasListado { get; private set; }
    public int ConsultasSugerencias { get; private set; }
    public int ConsultasDetalle { get; private set; }
    public int Transacciones { get; private set; }
    public int Confirmaciones { get; private set; }
    public string? TerminoRecibido { get; private set; }
    public EstadoOrden? EstadoRecibido { get; private set; }
    public DateOnly? FechaRecibida { get; private set; }
    public int? OrdenSeleccionadaRecibida { get; private set; }
    public Exception? ErrorLectura { get; set; }
    public Exception? ErrorDetalle { get; set; }

    public PuertosOrdenesApi() => Reiniciar();

    public void Reiniciar()
    {
        Ordenes.Clear();
        Detalles.Clear();
        ServiciosOrden.Clear();
        Productos.Clear();
        Servicios.Clear();
        Resumenes.Clear();
        DetallesConsulta.Clear();
        ConsultasListado = ConsultasSugerencias = ConsultasDetalle = Transacciones = Confirmaciones = 0;
        TerminoRecibido = null;
        EstadoRecibido = null;
        FechaRecibida = null;
        OrdenSeleccionadaRecibida = null;
        ErrorLectura = ErrorDetalle = null;

        Servicios.Add(new Servicio
        {
            Id = 1, CodigoCatalogo = "CAMBIO_ACEITE", Nombre = "Cambio de aceite",
            Descripcion = "Lubricación del motor", Costo = 25m, TiempoEstimadoHoras = 1m
        });
        Productos.Add(new Producto { Id = 1, Codigo = "FIL-001", Nombre = "Filtro de aceite", Precio = 12.50m, Stock = 10 });
        Productos.Add(new Producto { Id = 2, Codigo = "BUJ-001", Nombre = "Bujía", Precio = 7.20m, Stock = 10 });
        Ordenes.Add(new Orden
        {
            Id = 1, VehiculoId = 1, MecanicoId = 1, ServicioId = 1, CostoServicio = 25m,
            Fecha = new DateTime(2026, 10, 12), Estado = EstadoOrden.Activa, Total = 31m,
            Token = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            CreadoPor = "recepcion_prueba", FechaCreacion = new DateTime(2026, 10, 12, 8, 0, 0, DateTimeKind.Utc)
        });
        Detalles.Add(new DetalleOrden
        {
            Id = 1, OrdenId = 1, ProductoId = 1, Cantidad = 2, PrecioUnitario = 3m, Subtotal = 6m
        });
        Resumenes.Add(new OrdenResumen(1, new DateTime(2026, 10, 12), "123ABC", "Ana Arce Pérez",
            "José Muñoz", "Cambio de aceite", 31m, EstadoOrden.Activa, "recepcion_prueba"));
        DetallesConsulta[1] = new OrdenDetalle
        {
            Id = 1, Fecha = new DateTime(2026, 10, 12), Estado = EstadoOrden.Activa, Total = 31m,
            Placa = "123ABC", Marca = "Toyota", Modelo = "Corolla", NombreCliente = "Ana Arce Pérez",
            CiCliente = "1234567", NombreMecanico = "José Muñoz", CreadoPor = "recepcion_prueba",
            FechaCreacion = new DateTime(2026, 10, 12, 8, 0, 0, DateTimeKind.Utc),
            Servicios = [new ServicioOrdenDetalle { Nombre = "Cambio de aceite", Costo = 25m, Cantidad = 1 }],
            Productos = [new ProductoOrdenDetalle("FIL-001", "Filtro de aceite", 2, 3m, 6m)]
        };
    }

    public IReadOnlyList<OrdenResumen> Obtener(
        EstadoOrden? estado = null,
        DateOnly? fecha = null,
        string? terminoBusqueda = null,
        int? ordenSeleccionadaId = null)
    {
        ConsultasListado++;
        if (ErrorLectura is not null) throw ErrorLectura;
        EstadoRecibido = estado;
        FechaRecibida = fecha;
        TerminoRecibido = terminoBusqueda;
        OrdenSeleccionadaRecibida = ordenSeleccionadaId;
        return Resumenes
            .Where(orden => estado is null || orden.Estado == estado)
            .Where(orden => fecha is null || DateOnly.FromDateTime(orden.Fecha) == fecha)
            .Where(orden => terminoBusqueda is null ||
                orden.Id.ToString().Contains(terminoBusqueda, StringComparison.OrdinalIgnoreCase) ||
                orden.Placa.Contains(terminoBusqueda, StringComparison.OrdinalIgnoreCase))
            .Where(orden => ordenSeleccionadaId is null || orden.Id == ordenSeleccionadaId)
            .ToArray();
    }

    public Task<IReadOnlyList<SugerenciaOrden>> SugerirAsync(
        string termino,
        EstadoOrden? estado = null,
        DateOnly? fecha = null,
        CancellationToken cancellationToken = default)
    {
        ConsultasSugerencias++;
        if (ErrorLectura is not null) throw ErrorLectura;
        TerminoRecibido = termino;
        EstadoRecibido = estado;
        FechaRecibida = fecha;
        IReadOnlyList<SugerenciaOrden> sugerencias = Obtener(estado, fecha, termino)
            .Take(10)
            .Select(orden => new SugerenciaOrden(orden.Id, $"Orden #{orden.Id}", $"{orden.Placa} · {orden.NombreCliente}"))
            .ToArray();
        return Task.FromResult(sugerencias);
    }

    public OrdenDetalle? ObtenerDetalle(int id)
    {
        ConsultasDetalle++;
        if (ErrorDetalle is not null) throw ErrorDetalle;
        return DetallesConsulta.GetValueOrDefault(id);
    }

    public ITransaccionOrden Iniciar()
    {
        Transacciones++;
        return new TransaccionOrdenesApi(this);
    }

    private sealed class TransaccionOrdenesApi(PuertosOrdenesApi puertos) : ITransaccionOrden
    {
        public IOrdenRepository Ordenes { get; } = new OrdenesRepositorio(puertos);
        public IDetalleOrdenRepository Detalles { get; } = new DetallesRepositorio(puertos);
        public IProductoStockRepository Productos { get; } = new ProductosRepositorio(puertos);
        public ICabeceraOrdenRepository Cabecera { get; } = new CabeceraRepositorio(puertos);
        public void Confirmar() => puertos.Confirmaciones++;
        public void Revertir() { }
        public void Dispose() { }
    }

    private sealed class OrdenesRepositorio(PuertosOrdenesApi puertos) : IOrdenRepository
    {
        public Orden? ObtenerPorToken(Guid token) => puertos.Ordenes.Find(orden => orden.Token == token);
        public Orden? ObtenerParaAnular(int id) => puertos.Ordenes.Find(orden => orden.Id == id);

        public void Insertar(Orden orden)
        {
            orden.Id = puertos.Ordenes.Select(actual => actual.Id).DefaultIfEmpty().Max() + 1;
            puertos.Ordenes.Add(orden);
        }

        public void InsertarServicio(DetalleServicioOrden servicio) => puertos.ServiciosOrden.Add(servicio);

        public void ActualizarTotal(int ordenId, decimal total)
        {
            Orden? orden = puertos.Ordenes.Find(actual => actual.Id == ordenId);
            if (orden is not null) orden.Total = total;
        }

        public bool MarcarAnulada(int ordenId, string usuario, DateTime fechaUtc)
        {
            Orden? orden = puertos.Ordenes.Find(actual => actual.Id == ordenId);
            if (orden is null) return false;
            orden.Estado = EstadoOrden.Anulada;
            orden.AnuladoPor = usuario;
            orden.FechaAnulacion = fechaUtc;
            return true;
        }
    }

    private sealed class DetallesRepositorio(PuertosOrdenesApi puertos) : IDetalleOrdenRepository
    {
        public IReadOnlyList<DetalleOrden> ObtenerPorOrden(int ordenId) =>
            puertos.Detalles.Where(detalle => detalle.OrdenId == ordenId).ToArray();

        public void Insertar(DetalleOrden detalle)
        {
            detalle.Id = puertos.Detalles.Select(actual => actual.Id).DefaultIfEmpty().Max() + 1;
            puertos.Detalles.Add(detalle);
        }
    }

    private sealed class ProductosRepositorio(PuertosOrdenesApi puertos) : IProductoStockRepository
    {
        public IReadOnlyList<Producto> BloquearPorId(IEnumerable<int> ids) =>
            puertos.Productos.Where(producto => ids.Contains(producto.Id)).ToArray();

        public bool Descontar(int productoId, int cantidad)
        {
            Producto? producto = puertos.Productos.Find(actual => actual.Id == productoId);
            if (producto is null || producto.Stock < cantidad) return false;
            producto.Stock -= cantidad;
            return true;
        }

        public bool Restituir(int productoId, int cantidad)
        {
            Producto? producto = puertos.Productos.Find(actual => actual.Id == productoId);
            if (producto is null) return false;
            producto.Stock += cantidad;
            return true;
        }
    }

    private sealed class CabeceraRepositorio(PuertosOrdenesApi puertos) : ICabeceraOrdenRepository
    {
        public bool ExisteVehiculo(int vehiculoId, out int? clienteId)
        {
            clienteId = vehiculoId == 1 ? 1 : null;
            return vehiculoId == 1;
        }

        public bool ExisteMecanico(int mecanicoId) => mecanicoId == 1;

        public bool ExisteServicio(int servicioId, out Servicio? servicio)
        {
            servicio = puertos.Servicios.Find(actual => actual.Id == servicioId);
            return servicio is not null;
        }
    }
}

internal sealed class ComprobantePdfPrueba : IComprobantePdf
{
    public byte[] Generar(OrdenDetalle orden, string totalLiteral, string generadoPor, DateTimeOffset fechaGeneracion) =>
        "%PDF-prueba"u8.ToArray();
}

internal sealed class BaseDatosOrdenesApiException() : DbException("Detalle de persistencia que no debe salir de la API.");
