using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TallerMecanico.Api;
using TallerMecanico.Contratos.Ordenes;
using TallerMecanico.Aplicacion.CasosUso.Ordenes.Contratos;
using TallerMecanico.Aplicacion.Dto;

namespace TallerMecanico.Pages.Ordenes;

[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class CrearModel(OrdenesApi ordenes, IBorradorOrden borradores, ITokenOrden tokens,
    VehiculosApi vehiculos, MecanicosApi mecanicos, ProductosApi productos, ClientesApi clientes,
    ServiciosApi servicios) : PageModel
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

    public VehiculoDto? VehiculoSeleccionado { get; private set; }
    public MecanicoDto? MecanicoSeleccionado { get; private set; }
    public ProductoDto? ProductoSeleccionado { get; private set; }
    public ServicioDto? ServicioSeleccionado { get; private set; }
    public IReadOnlyList<ServicioSeleccionadoOrdenViewModel> ServiciosSeleccionados { get; private set; } = [];
    public IReadOnlyList<LineaOrdenTemporalViewModel> Lineas { get; private set; } = [];
    public decimal Total => Lineas.Sum(x => x.Precio * x.Cantidad);
    public long CantidadTotal => Lineas.Sum(x => (long)x.Cantidad);
    public decimal CostoServiciosSeleccionados => ServiciosSeleccionados.Sum(x => x.Subtotal);
    public decimal TotalEstimado => Total + CostoServiciosSeleccionados;
    public string? Mensaje { get; private set; }
    public int? OrdenCreadaId { get; private set; }
    public DateTime FechaOrden { get; private set; }

    public async Task<JsonResult> OnGetSugerenciasVehiculosAsync(string? termino) =>
        string.IsNullOrWhiteSpace(termino)
            ? new JsonResult((await vehiculos.ObtenerAsync(cancelacion: HttpContext.RequestAborted)).Select(vehiculo => new SugerenciaVehiculo(
                vehiculo.Id, vehiculo.Placa, $"{vehiculo.Marca} {vehiculo.Modelo}".Trim())))
            : new JsonResult(await vehiculos.SugerirAsync(termino, HttpContext.RequestAborted));

    public async Task<JsonResult> OnGetSugerenciasServiciosAsync(string? termino) =>
        string.IsNullOrWhiteSpace(termino)
            ? new JsonResult((await servicios.ObtenerCatalogoAsync(HttpContext.RequestAborted)).Select(servicio => new SugerenciaServicio(
                servicio.Id, servicio.Nombre, servicio.Descripcion)))
            : new JsonResult(await servicios.SugerirCatalogoAsync(termino, HttpContext.RequestAborted));

    public async Task<JsonResult> OnGetSugerenciasMecanicosAsync(string? termino) =>
        string.IsNullOrWhiteSpace(termino)
            ? new JsonResult((await mecanicos.ObtenerAsync(cancelacion: HttpContext.RequestAborted)).Select(mecanico => new SugerenciaMecanico(
                mecanico.Id, $"{mecanico.Nombres} {mecanico.PrimerApellido} {mecanico.SegundoApellido}".Trim(), mecanico.Ci)))
            : new JsonResult(await mecanicos.SugerirAsync(termino, HttpContext.RequestAborted, incluirEspecialidad: false));

    public async Task<JsonResult> OnGetSugerenciasProductosAsync(string? termino) =>
        new(await productos.SugerirAsync(termino, HttpContext.RequestAborted));

    public async Task<IActionResult> OnGetServicioSeleccionadoAsync(int id)
    {
        var servicio = id > 0 ? await ObtenerServicioCatalogoAsync(id) : null;
        return servicio is null ? NotFound() : new JsonResult(new
        {
            servicio.Id, servicio.Nombre, servicio.Descripcion, servicio.Costo, servicio.TiempoEstimadoHoras, servicio.UnidadCantidad
        });
    }

    public async Task<IActionResult> OnGetVehiculoSeleccionadoAsync(int id)
    {
        var vehiculo = id > 0 ? await vehiculos.ObtenerPorIdAsync(id) : null;
        return vehiculo is null ? NotFound() : new JsonResult(new { vehiculo.Id, RequiereCliente = !vehiculo.ClienteId.HasValue });
    }

    public async Task<IActionResult> OnGetProductoSeleccionadoAsync(int id)
    {
        ProductoDto? producto = id > 0 ? await productos.ObtenerPorIdAsync(id) : null;
        return producto is null ? NotFound() : new JsonResult(new
        {
            producto.Id, producto.Nombre, producto.Codigo, producto.Stock
        });
    }

    public async Task<IActionResult> OnGetAsync()
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
        if (Formulario.Token == Guid.Empty) Formulario.Token = tokens.Generar();
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
        if (ProductoNuevoId.HasValue && (await productos.ObtenerPorIdAsync(ProductoNuevoId.Value)) is { } nuevo)
            AgregarProducto(nuevo, 1);
        await CargarAsync();
        Mensaje = TempData?["OrdenMensaje"] as string;
        if (ClienteNuevoId.HasValue)
            Mensaje = TempData?["ClienteExito"] as string ?? Mensaje;
        if (ProductoNuevoId.HasValue)
            Mensaje = TempData?["ProductoExito"] as string ?? Mensaje;
        if (TempData?["OrdenError"] is string error && !string.IsNullOrWhiteSpace(error))
            ModelState.AddModelError(string.Empty, error);
        if (TempData?.TryGetValue("OrdenCreadaId", out object? creada) == true && creada is int creadaId)
            OrdenCreadaId = creadaId;
        return Page();
    }

    public async Task<IActionResult> OnPostAgregarLineaAsync()
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
        ProductoDto? producto = await productos.ObtenerPorIdAsync(ProductoId);
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
        if (!datosValidos) { await CargarAsync(); return Page(); }
        Formulario.Lineas.RemoveAll(x => x.ProductoId == producto!.Id);
        Formulario.Lineas.Add(new() { ProductoId = producto!.Id, Cantidad = Cantidad, Precio = producto.Precio });
        TempData["OrdenMensaje"] = $"{producto.Nombre} se añadió a la orden.";
        return await PersistirYVolverAsync();
    }

    public async Task<IActionResult> OnPostActualizarCantidadAsync(int productoId, int cantidad)
    {
        RestaurarContextoCliente();
        var linea = Formulario.Lineas.FirstOrDefault(x => x.ProductoId == productoId);
        ProductoDto? producto = await productos.ObtenerPorIdAsync(productoId);
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
        if (!cantidadValida) { await CargarAsync(); return Page(); }
        linea.Cantidad = cantidad;
        TempData["OrdenMensaje"] = "La cantidad del repuesto se actualizó.";
        return await PersistirYVolverAsync();
    }

    public async Task<IActionResult> OnPostQuitarLineaAsync(int productoId)
    {
        RestaurarContextoCliente();
        Formulario.Lineas.RemoveAll(x => x.ProductoId == productoId);
        TempData["OrdenMensaje"] = "El repuesto se quitó de la orden.";
        return await PersistirYVolverAsync();
    }

    public async Task<IActionResult> OnPostGuardarAsync()
    {
        RestaurarContextoCliente();
        await CargarAsync();
        if (Formulario.ServicioIds.Count == 0)
            ModelState.AddModelError("Formulario.ServicioIds", "Agrega al menos un servicio a la orden.");
        if (!tokens.EsValido(Formulario.Token))
            ModelState.AddModelError(string.Empty, "El formulario venció o no pertenece a esta sesión. Revisa las órdenes antes de iniciar uno nuevo.");
        if (!ModelState.IsValid) return Page();
        var solicitud = new CrearOrdenDto(Formulario.VehiculoId, Formulario.MecanicoId, FechaOrden,
            Formulario.Token, Formulario.Lineas.Select(x => new LineaOrdenDto(x.ProductoId, x.Cantidad)).ToArray(),
            Formulario.ServicioIds[0], Formulario.ServicioIds.ToArray(),
            ServiciosSeleccionados.Select(servicio => new LineaServicioOrdenDto(servicio.Id, servicio.Cantidad)).ToArray());
        ResultadoOrdenDto resultado = await ordenes.CrearAsync(solicitud);
        if (!resultado.Exito)
        {
            foreach (var error in resultado.Errores) ModelState.AddModelError(MapearError(error.Key), error.Value);
            await CargarAsync();
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

    public IActionResult OnPostNuevoMecanico()
    {
        if (!User.IsInRole("Administrador")) return Forbid();
        Guid id = borradores.Guardar(Formulario);
        return RedirectToPage("/Mecanicos/Index", new { OrdenBorradorId = id });
    }

    private async Task<IActionResult> PersistirYVolverAsync()
    {
        if (Formulario.Token == Guid.Empty) Formulario.Token = tokens.Generar();
        foreach (var linea in Formulario.Lineas)
            if ((await productos.ObtenerPorIdAsync(linea.ProductoId)) is { } producto) linea.Precio = producto.Precio;
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

    private async Task<ServicioDto?> ObtenerServicioCatalogoAsync(int id) =>
        (await servicios.ObtenerCatalogoAsync(HttpContext.RequestAborted)).FirstOrDefault(servicio => servicio.Id == id);

    private async Task CargarAsync()
    {
        FechaOrden = await ordenes.ObtenerFechaActualAsync(HttpContext.RequestAborted);
        VehiculoSeleccionado = Formulario.VehiculoId > 0 ? await vehiculos.ObtenerPorIdAsync(Formulario.VehiculoId) : null;
        if (VehiculoSeleccionado is not null) BuscarVehiculo = VehiculoSeleccionado.Placa;
        MecanicoSeleccionado = Formulario.MecanicoId > 0 ? await mecanicos.ObtenerPorIdAsync(Formulario.MecanicoId) : null;
        if (MecanicoSeleccionado is not null)
            TextoMecanico = string.Join(" ", new[] { MecanicoSeleccionado.Nombres, MecanicoSeleccionado.PrimerApellido, MecanicoSeleccionado.SegundoApellido }
                .Where(nombre => !string.IsNullOrWhiteSpace(nombre)));
        var catalogo = await servicios.ObtenerCatalogoAsync(HttpContext.RequestAborted);
        ServicioSeleccionado = catalogo.FirstOrDefault(servicio => servicio.Id == Formulario.ServicioId);
        if (ServicioSeleccionado is not null && string.IsNullOrWhiteSpace(TextoServicio))
            TextoServicio = ServicioSeleccionado.Nombre;
        ServiciosSeleccionados = Formulario.ServicioIds.Distinct()
            .Select(id => catalogo.FirstOrDefault(servicio => servicio.Id == id))
            .Where(servicio => servicio is not null)
            .Select(servicio => new ServicioSeleccionadoOrdenViewModel
            {
                Id = servicio!.Id, Nombre = servicio.Nombre, Descripcion = servicio.Descripcion,
                Costo = servicio.Costo, TiempoEstimadoHoras = servicio.TiempoEstimadoHoras,
                UnidadCantidad = servicio.UnidadCantidad,
                Cantidad = Formulario.CantidadesServicios.GetValueOrDefault(servicio.Id, 1)
            }).ToArray();
        ProductoSeleccionado = ProductoId > 0 ? await productos.ObtenerPorIdAsync(ProductoId) : null;
        if (ProductoSeleccionado is not null) BuscarProducto = ProductoSeleccionado.Nombre;
        var lineas = new List<LineaOrdenTemporalViewModel>();
        foreach (var linea in Formulario.Lineas)
        {
            if (await productos.ObtenerPorIdAsync(linea.ProductoId) is not { } producto) continue;
            linea.Precio = producto.Precio;
            lineas.Add(new() { ProductoId = producto.Id, Codigo = producto.Codigo, Nombre = producto.Nombre,
                Cantidad = linea.Cantidad, Stock = producto.Stock, Precio = producto.Precio });
        }
        Lineas = lineas;
        NombreClienteNuevo = Formulario.ClienteNuevoId.HasValue &&
            await clientes.ObtenerPorIdAsync(Formulario.ClienteNuevoId.Value) is { } cliente
                ? $"{cliente.PrimerApellido} {cliente.SegundoApellido} {cliente.Nombres}".Trim() : null;
    }

    public string? NombreClienteNuevo { get; private set; }

    private void AgregarProducto(ProductoDto producto, int cantidad)
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
