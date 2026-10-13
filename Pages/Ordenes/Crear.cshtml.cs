using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TallerMecanico.Aplicacion.Fachadas;
using TallerMecanico.Aplicacion.CasosUso.Ordenes;
using TallerMecanico.Aplicacion.Puertos;
using TallerMecanico.Dominio.Entidades;
using TallerMecanico.Aplicacion.Servicios;
using TallerMecanico.Aplicacion.Dto;

namespace TallerMecanico.Pages.Ordenes;

public sealed class CrearModel(OrdenServicioFacade facade, IBorradorOrdenPort borradores,
    VehiculoService vehiculos, MecanicoService mecanicos, ProductoService productos, ClienteService clientes,
    IServicioService servicios) : PageModel
{
    [BindProperty] public OrdenFormViewModel Formulario { get; set; } = new();
    [BindProperty(SupportsGet = true)] public Guid? BorradorId { get; set; }
    [BindProperty(SupportsGet = true)] public int? VehiculoNuevoId { get; set; }
    [BindProperty(SupportsGet = true)] public int? ProductoNuevoId { get; set; }
    [BindProperty(SupportsGet = true)] public int? ClienteNuevoId { get; set; }
    [BindProperty] public int ProductoId { get; set; }
    [BindProperty] public int Cantidad { get; set; } = 1;
    [BindProperty(SupportsGet = true)] public string? BuscarVehiculo { get; set; }
    [BindProperty(SupportsGet = true)] public string? BuscarProducto { get; set; }

    public IReadOnlyList<Vehiculo> Vehiculos { get; private set; } = [];
    public IReadOnlyList<Mecanico> Mecanicos { get; private set; } = [];
    public IReadOnlyList<Producto> Productos { get; private set; } = [];
    public IReadOnlyList<Servicio> Servicios { get; private set; } = [];
    public IReadOnlyList<LineaOrdenTemporalViewModel> Lineas { get; private set; } = [];
    public decimal Total => Lineas.Sum(x => x.Precio * x.Cantidad);
    public long CantidadTotal => Lineas.Sum(x => (long)x.Cantidad);
    public decimal CostoServicioSeleccionado =>
        Formulario.ServicioId.HasValue
            ? Servicios.FirstOrDefault(x => x.Id == Formulario.ServicioId.Value)?.Costo ?? 0
            : 0;
    public decimal TotalEstimado => Total + CostoServicioSeleccionado;
    public string? Mensaje { get; private set; }
    public int? OrdenCreadaId { get; private set; }

    public IActionResult OnGet()
    {
        if (BorradorId.HasValue)
        {
            var guardado = borradores.Obtener(BorradorId.Value);
            if (guardado is null) { TempData["OrdenError"] = "El borrador venció. Inicia una orden nueva."; return RedirectToPage(); }
            Formulario = guardado;
        }
        if (Formulario.Token == Guid.Empty) Formulario.Token = facade.GenerarTokenFormulario();
        if (VehiculoNuevoId.HasValue) Formulario.VehiculoId = VehiculoNuevoId.Value;
        if (ClienteNuevoId.HasValue)
        {
            Formulario.ClienteNuevoId = ClienteNuevoId.Value;
            if (BorradorId.HasValue)
            {
                borradores.Eliminar(BorradorId.Value);
                BorradorId = borradores.Guardar(Formulario);
            }
        }
        if (ProductoNuevoId.HasValue && productos.ObtenerPorId(ProductoNuevoId.Value) is { } nuevo)
            AgregarProducto(nuevo, 1);
        Cargar();
        Mensaje = TempData?["OrdenMensaje"] as string ?? TempData?["OrdenError"] as string;
        if (TempData?.TryGetValue("OrdenCreadaId", out object? creada) == true && creada is int creadaId)
            OrdenCreadaId = creadaId;
        return Page();
    }

    public IActionResult OnPostAgregarLinea()
    {
        RestaurarContextoCliente();
        if (ProductoId <= 0 || Cantidad <= 0) ModelState.AddModelError(nameof(Cantidad), "La cantidad debe ser mayor que cero.");
        Producto? producto = productos.ObtenerPorId(ProductoId);
        if (producto is null) ModelState.AddModelError(nameof(ProductoId), "Selecciona un producto válido.");
        if (!ModelState.IsValid) { Cargar(); return Page(); }
        Formulario.Lineas.RemoveAll(x => x.ProductoId == producto!.Id);
        Formulario.Lineas.Add(new() { ProductoId = producto!.Id, Cantidad = Cantidad, Precio = producto.Precio });
        return PersistirYVolver();
    }

    public IActionResult OnPostActualizarCantidad(int productoId, int cantidad)
    {
        RestaurarContextoCliente();
        var linea = Formulario.Lineas.FirstOrDefault(x => x.ProductoId == productoId);
        Producto? producto = productos.ObtenerPorId(productoId);
        if (linea is null || producto is null) return BadRequest();
        if (cantidad <= 0) ModelState.AddModelError(nameof(Cantidad), "La cantidad debe ser mayor que cero.");
        if (!ModelState.IsValid) { Cargar(); return Page(); }
        linea.Cantidad = cantidad;
        return PersistirYVolver();
    }

    public IActionResult OnPostQuitarLinea(int productoId)
    {
        RestaurarContextoCliente();
        Formulario.Lineas.RemoveAll(x => x.ProductoId == productoId);
        return PersistirYVolver();
    }

    public IActionResult OnPostGuardar()
    {
        RestaurarContextoCliente();
        Cargar();
        if (Formulario.Lineas.Count == 0 && !Formulario.ServicioId.HasValue)
            ModelState.AddModelError("", "Agrega al menos un producto o selecciona un servicio.");
        if (!ModelState.IsValid) return Page();
        var solicitud = new CrearOrdenSolicitud(Formulario.VehiculoId, Formulario.MecanicoId, Formulario.Fecha,
            Formulario.Token, Formulario.Lineas.Select(x => new LineaOrdenSolicitud(x.ProductoId, x.Cantidad)).ToArray(),
            Formulario.ServicioId);
        ResultadoOrden resultado = facade.CrearOrden(solicitud);
        if (!resultado.Exito)
        {
            foreach (var error in resultado.Errores) ModelState.AddModelError(MapearError(error.Key), error.Value);
            Cargar();
            return Page();
        }
        borradores.Eliminar(BorradorId ?? Guid.Empty);
        TempData["OrdenMensaje"] = resultado.EsReenvio
            ? $"La orden #{resultado.OrdenId} ya se había guardado. Total: Bs. {resultado.Total:0.00}."
            : $"Orden #{resultado.OrdenId} guardada correctamente. Total: Bs. {resultado.Total:0.00}.";
        TempData["OrdenCreadaId"] = resultado.OrdenId;
        return RedirectToPage("./Index");
    }

    public IActionResult OnPostNuevoVehiculo()
    {
        Guid id = borradores.Guardar(Formulario);
        return RedirectToPage("/Vehiculos/Index", new { OrdenBorradorId = id, ClientePreseleccionadoId = Formulario.ClienteNuevoId });
    }

    public IActionResult OnPostNuevoCliente()
    {
        Guid id = borradores.Guardar(Formulario);
        return RedirectToPage("/Clientes/Index", new { OrdenBorradorId = id });
    }

    public IActionResult OnPostNuevoProducto()
    {
        Guid id = borradores.Guardar(Formulario);
        return RedirectToPage("/Productos/Index", new { OrdenBorradorId = id });
    }

    private IActionResult PersistirYVolver()
    {
        if (Formulario.Token == Guid.Empty) Formulario.Token = facade.GenerarTokenFormulario();
        foreach (var linea in Formulario.Lineas)
            if (productos.ObtenerPorId(linea.ProductoId) is { } producto) linea.Precio = producto.Precio;
        if (BorradorId.HasValue) borradores.Eliminar(BorradorId.Value);
        BorradorId = borradores.Guardar(Formulario);
        return RedirectToPage(new { BorradorId });
    }

    private void RestaurarContextoCliente()
    {
        if (!Formulario.ClienteNuevoId.HasValue && BorradorId.HasValue &&
            borradores.Obtener(BorradorId.Value) is { ClienteNuevoId: not null } guardado)
            Formulario.ClienteNuevoId = guardado.ClienteNuevoId;
    }

    private void Cargar()
    {
        Vehiculos = vehiculos.GetAll();
        Mecanicos = mecanicos.Obtener();
        Servicios = servicios.ObtenerTodos();
        Productos = string.IsNullOrWhiteSpace(BuscarProducto) ? productos.Obtener() : productos.Buscar(BuscarProducto);
        Lineas = Formulario.Lineas.Select(x =>
        {
            Producto? p = productos.ObtenerPorId(x.ProductoId);
            if (p is null) return null;
            x.Precio = p.Precio;
            return new LineaOrdenTemporalViewModel { ProductoId = p.Id, Codigo = p.Codigo, Nombre = p.Nombre, Cantidad = x.Cantidad, Stock = p.Stock, Precio = p.Precio };
        }).Where(x => x is not null).Select(x => x!).ToArray();
    }

    public string? NombreClienteNuevo => Formulario.ClienteNuevoId.HasValue
        ? clientes.ObtenerPorId(Formulario.ClienteNuevoId.Value) is { } cliente
            ? $"{cliente.PrimerApellido} {cliente.SegundoApellido} {cliente.Nombres}".Trim()
            : null
        : null;

    private void AgregarProducto(Producto producto, int cantidad)
    {
        Formulario.Lineas.RemoveAll(x => x.ProductoId == producto.Id);
        Formulario.Lineas.Add(new() { ProductoId = producto.Id, Cantidad = cantidad, Precio = producto.Precio });
    }

    private static string MapearError(string campo) => campo switch
    {
        "VehiculoId" => "Formulario.VehiculoId", "MecanicoId" => "Formulario.MecanicoId",
        "Fecha" => "Formulario.Fecha", "ServicioId" => "Formulario.ServicioId",
        _ => ""
    };
}
