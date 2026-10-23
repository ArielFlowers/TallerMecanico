using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TallerMecanico.Aplicacion.Fachadas;
using TallerMecanico.Aplicacion.CasosUso.Ordenes;
using TallerMecanico.Aplicacion.Puertos;
using TallerMecanico.Dominio.Entidades;
using TallerMecanico.Aplicacion.Servicios;
using TallerMecanico.Aplicacion.Dto;

namespace TallerMecanico.Pages.Ordenes;

[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
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
    [BindProperty(SupportsGet = true)] public int ScrollY { get; set; }
    [BindProperty(SupportsGet = true)] public string? BuscarVehiculo { get; set; }
    [BindProperty(SupportsGet = true)] public string? BuscarProducto { get; set; }
    [BindProperty] public string? TextoServicio { get; set; }
    [BindProperty] public string? TextoMecanico { get; set; }

    public Vehiculo? VehiculoSeleccionado { get; private set; }
    public Mecanico? MecanicoSeleccionado { get; private set; }
    public Producto? ProductoSeleccionado { get; private set; }
    public Servicio? ServicioSeleccionado { get; private set; }
    public IReadOnlyList<ServicioSeleccionadoOrdenViewModel> ServiciosSeleccionados { get; private set; } = [];
    public IReadOnlyList<LineaOrdenTemporalViewModel> Lineas { get; private set; } = [];
    public decimal Total => Lineas.Sum(x => x.Precio * x.Cantidad);
    public long CantidadTotal => Lineas.Sum(x => (long)x.Cantidad);
    public decimal CostoServiciosSeleccionados => ServiciosSeleccionados.Sum(x => x.Subtotal);
    public decimal TotalEstimado => Total + CostoServiciosSeleccionados;
    public string? Mensaje { get; private set; }
    public int? OrdenCreadaId { get; private set; }
    public DateTime FechaOrden => facade.FechaActual;

    public async Task<JsonResult> OnGetSugerenciasVehiculosAsync(string? termino) =>
        string.IsNullOrWhiteSpace(termino)
            ? new JsonResult(vehiculos.GetAll().Select(vehiculo => new SugerenciaVehiculo(
                vehiculo.Id, vehiculo.Placa, $"{vehiculo.Marca} {vehiculo.Modelo}".Trim())))
            : new JsonResult(await vehiculos.SugerirAsync(termino, HttpContext.RequestAborted));

    public async Task<JsonResult> OnGetSugerenciasServiciosAsync(string? termino) =>
        string.IsNullOrWhiteSpace(termino)
            ? new JsonResult(servicios.ObtenerCatalogo().Select(servicio => new SugerenciaServicio(
                servicio.Id, servicio.Nombre, servicio.Descripcion)))
            : new JsonResult(await servicios.SugerirCatalogoAsync(termino, HttpContext.RequestAborted));

    public async Task<JsonResult> OnGetSugerenciasMecanicosAsync(string? termino) =>
        string.IsNullOrWhiteSpace(termino)
            ? new JsonResult(mecanicos.Obtener().Select(mecanico => new SugerenciaMecanico(
                mecanico.Id, $"{mecanico.Nombres} {mecanico.PrimerApellido} {mecanico.SegundoApellido}".Trim(), mecanico.Ci)))
            : new JsonResult(await mecanicos.SugerirAsync(termino, HttpContext.RequestAborted, incluirEspecialidad: false));

    public async Task<JsonResult> OnGetSugerenciasProductosAsync(string? termino) =>
        new(await productos.SugerirAsync(termino, HttpContext.RequestAborted));

    public IActionResult OnGetServicioSeleccionado(int id)
    {
        var servicio = id > 0 ? servicios.ObtenerDelCatalogo(id) : null;
        return servicio is null ? NotFound() : new JsonResult(new
        {
            servicio.Id, servicio.Nombre, servicio.Descripcion, servicio.Costo, servicio.TiempoEstimadoHoras, servicio.UnidadCantidad
        });
    }

    public IActionResult OnGetVehiculoSeleccionado(int id)
    {
        var vehiculo = id > 0 ? vehiculos.GetById(id) : null;
        return vehiculo is null ? NotFound() : new JsonResult(new { vehiculo.Id, RequiereCliente = !vehiculo.ClienteId.HasValue });
    }

    public IActionResult OnGetProductoSeleccionado(int id)
    {
        Producto? producto = id > 0 ? productos.ObtenerPorId(id) : null;
        return producto is null ? NotFound() : new JsonResult(new
        {
            producto.Id, producto.Nombre, producto.Codigo, producto.Stock
        });
    }

    public IActionResult OnGet()
    {
        if (BorradorId.HasValue)
        {
            var guardado = borradores.Obtener(BorradorId.Value);
            if (guardado is null) { TempData["OrdenError"] = "El borrador venció. Inicia una orden nueva."; return RedirectToPage(); }
            Formulario = guardado;
        }
        if (Formulario.ServicioIds.Count == 0 && Formulario.ServicioId.HasValue)
        {
            Formulario.ServicioIds.Add(Formulario.ServicioId.Value);
            Formulario.ServicioId = null;
            TextoServicio = null;
        }
        Formulario.ServicioIds = Formulario.ServicioIds.Where(id => id > 0).Distinct().ToList();
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
        bool datosValidos = true;
        if (ProductoId <= 0)
        {
            ModelState.AddModelError(nameof(ProductoId), "Selecciona un producto de la lista.");
            datosValidos = false;
        }
        if (Cantidad <= 0)
        {
            ModelState.AddModelError(nameof(Cantidad), "La cantidad debe ser mayor que cero.");
            datosValidos = false;
        }
        Producto? producto = productos.ObtenerPorId(ProductoId);
        if (ProductoId > 0 && producto is null)
        {
            ModelState.AddModelError(nameof(ProductoId), "El producto seleccionado ya no existe. Vuelve a elegirlo.");
            datosValidos = false;
        }
        else if (producto is not null && producto.Stock <= 0)
        {
            ModelState.AddModelError(nameof(ProductoId), $"No hay stock disponible de {producto.Nombre}.");
            datosValidos = false;
        }
        else if (producto is not null && Cantidad > producto.Stock)
        {
            ModelState.AddModelError(nameof(Cantidad), $"Stock insuficiente: hay {producto.Stock} unidades disponibles.");
            datosValidos = false;
        }
        // Esta operación valida el repuesto, no todos los campos aún incompletos de la orden.
        if (!datosValidos) { Cargar(); return Page(); }
        Formulario.Lineas.RemoveAll(x => x.ProductoId == producto!.Id);
        Formulario.Lineas.Add(new() { ProductoId = producto!.Id, Cantidad = Cantidad, Precio = producto.Precio });
        TempData["OrdenMensaje"] = $"{producto.Nombre} se añadió a la orden.";
        return PersistirYVolver();
    }

    public IActionResult OnPostActualizarCantidad(int productoId, int cantidad)
    {
        RestaurarContextoCliente();
        var linea = Formulario.Lineas.FirstOrDefault(x => x.ProductoId == productoId);
        Producto? producto = productos.ObtenerPorId(productoId);
        if (linea is null || producto is null) return BadRequest();
        bool cantidadValida = true;
        if (cantidad <= 0)
        {
            ModelState.AddModelError(nameof(Cantidad), "La cantidad debe ser mayor que cero.");
            cantidadValida = false;
        }
        else if (producto.Stock <= 0)
        {
            ModelState.AddModelError(nameof(Cantidad), $"No hay stock disponible de {producto.Nombre}.");
            cantidadValida = false;
        }
        else if (cantidad > producto.Stock)
        {
            ModelState.AddModelError(nameof(Cantidad), $"Stock insuficiente: hay {producto.Stock} unidades disponibles.");
            cantidadValida = false;
        }
        if (!cantidadValida) { Cargar(); return Page(); }
        linea.Cantidad = cantidad;
        TempData["OrdenMensaje"] = "La cantidad del repuesto se actualizó.";
        return PersistirYVolver();
    }

    public IActionResult OnPostQuitarLinea(int productoId)
    {
        RestaurarContextoCliente();
        Formulario.Lineas.RemoveAll(x => x.ProductoId == productoId);
        TempData["OrdenMensaje"] = "El repuesto se quitó de la orden.";
        return PersistirYVolver();
    }

    public IActionResult OnPostGuardar()
    {
        RestaurarContextoCliente();
        Cargar();
        if (Formulario.ServicioIds.Count == 0)
            ModelState.AddModelError("Formulario.ServicioIds", "Agrega al menos un servicio a la orden.");
        if (!ModelState.IsValid) return Page();
        var solicitud = new CrearOrdenSolicitud(Formulario.VehiculoId, Formulario.MecanicoId, FechaOrden,
            Formulario.Token, Formulario.Lineas.Select(x => new LineaOrdenSolicitud(x.ProductoId, x.Cantidad)).ToArray(),
            Formulario.ServicioIds[0], Formulario.ServicioIds.ToArray(),
            ServiciosSeleccionados.Select(servicio => new LineaServicioOrdenSolicitud(servicio.Id, servicio.Cantidad)).ToArray());
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
        return RedirectToPage(new { BorradorId, ScrollY });
    }

    private void RestaurarContextoCliente()
    {
        if (!Formulario.ClienteNuevoId.HasValue && BorradorId.HasValue &&
            borradores.Obtener(BorradorId.Value) is { ClienteNuevoId: not null } guardado)
            Formulario.ClienteNuevoId = guardado.ClienteNuevoId;
    }

    private void Cargar()
    {
        VehiculoSeleccionado = Formulario.VehiculoId > 0 ? vehiculos.GetById(Formulario.VehiculoId) : null;
        if (VehiculoSeleccionado is not null) BuscarVehiculo = VehiculoSeleccionado.Placa;
        MecanicoSeleccionado = Formulario.MecanicoId > 0 ? mecanicos.ObtenerPorId(Formulario.MecanicoId).SingleOrDefault() : null;
        if (MecanicoSeleccionado is not null)
            TextoMecanico = string.Join(" ", new[] { MecanicoSeleccionado.Nombres, MecanicoSeleccionado.PrimerApellido, MecanicoSeleccionado.SegundoApellido }
                .Where(nombre => !string.IsNullOrWhiteSpace(nombre)));
        ServicioSeleccionado = Formulario.ServicioId.HasValue ? servicios.ObtenerDelCatalogo(Formulario.ServicioId.Value) : null;
        if (ServicioSeleccionado is not null && string.IsNullOrWhiteSpace(TextoServicio))
            TextoServicio = ServicioSeleccionado.Nombre;
        ServiciosSeleccionados = Formulario.ServicioIds
            .Distinct()
            .Select(servicios.ObtenerDelCatalogo)
            .Where(servicio => servicio is not null)
            .Select(servicio => new ServicioSeleccionadoOrdenViewModel
            {
                Id = servicio!.Id,
                Nombre = servicio.Nombre,
                Descripcion = servicio.Descripcion,
                Costo = servicio.Costo,
                TiempoEstimadoHoras = servicio.TiempoEstimadoHoras,
                UnidadCantidad = servicio.UnidadCantidad,
                Cantidad = Formulario.CantidadesServicios.GetValueOrDefault(servicio.Id, 1)
            })
            .ToArray();
        ProductoSeleccionado = ProductoId > 0 ? productos.ObtenerPorId(ProductoId) : null;
        if (ProductoSeleccionado is not null) BuscarProducto = ProductoSeleccionado.Nombre;
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
        "Fecha" => "Formulario.Fecha", "ServicioId" or "ServicioIds" => "Formulario.ServicioIds",
        _ => ""
    };
}
