using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TallerMecanico.Api;
using TallerMecanico.Sesion;
using TallerMecanico.Contratos.Catalogos;
using TallerMecanico.Contratos.Ordenes;

namespace TallerMecanico.Pages.Ordenes;

[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class CrearModel(
    OrdenesApi ordenes,
    ClientesApi clientes,
    VehiculosApi vehiculos,
    MecanicosApi mecanicos,
    ProductosApi productos,
    ServiciosApi servicios,
    IBorradorOrden borradores,
    ITokenOrden tokens) : PageModel
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
    public DateTime FechaOrden { get; private set; } = DateTime.Today;
    public string? NombreClienteNuevo { get; private set; }

    public async Task<JsonResult> OnGetSugerenciasVehiculosAsync(string? termino) =>
        string.IsNullOrWhiteSpace(termino)
            ? new JsonResult((await vehiculos.ObtenerAsync(cancelacion: HttpContext.RequestAborted))
                .Select(vehiculo => new SugerenciaVehiculo(
                    vehiculo.Id, vehiculo.Placa, $"{vehiculo.Marca} {vehiculo.Modelo}".Trim())))
            : new JsonResult(await vehiculos.SugerirAsync(termino, HttpContext.RequestAborted));

    // Sin término muestra todo el catálogo; con término usa las reglas del buscador del servicio.
    public async Task<JsonResult> OnGetSugerenciasServiciosAsync(string? termino) =>
        string.IsNullOrWhiteSpace(termino)
            ? new JsonResult((await servicios.ObtenerCatalogoAsync(HttpContext.RequestAborted))
                .Select(servicio => new SugerenciaServicio(servicio.Id, servicio.Nombre, servicio.Descripcion)))
            : new JsonResult(await servicios.SugerirCatalogoAsync(termino, HttpContext.RequestAborted));

    public async Task<JsonResult> OnGetSugerenciasMecanicosAsync(string? termino) =>
        string.IsNullOrWhiteSpace(termino)
            ? new JsonResult((await mecanicos.ObtenerAsync(cancelacion: HttpContext.RequestAborted))
                .Select(mecanico => new SugerenciaMecanico(
                    mecanico.Id, NombreCompleto(mecanico), mecanico.Ci)))
            : new JsonResult(await mecanicos.SugerirAsync(termino, HttpContext.RequestAborted, incluirEspecialidad: false));

    public async Task<JsonResult> OnGetSugerenciasProductosAsync(string? termino) =>
        new(await productos.SugerirAsync(termino, HttpContext.RequestAborted));

    public async Task<IActionResult> OnGetServicioSeleccionadoAsync(int id)
    {
        var catalogo = await servicios.ObtenerCatalogoAsync(HttpContext.RequestAborted);
        var servicio = id > 0 ? catalogo.FirstOrDefault(actual => actual.Id == id) : null;
        return servicio is null ? NotFound() : new JsonResult(new
        {
            servicio.Id, servicio.Nombre, servicio.Descripcion, servicio.Costo,
            servicio.TiempoEstimadoHoras, servicio.UnidadCantidad
        });
    }

    public async Task<IActionResult> OnGetVehiculoSeleccionadoAsync(int id)
    {
        var vehiculo = id > 0 ? await vehiculos.ObtenerPorIdAsync(id, HttpContext.RequestAborted) : null;
        return vehiculo is null ? NotFound() : new JsonResult(new
        {
            vehiculo.Id, RequiereCliente = !vehiculo.ClienteId.HasValue
        });
    }

    public async Task<IActionResult> OnGetProductoSeleccionadoAsync(int id)
    {
        var producto = id > 0 ? await productos.ObtenerPorIdAsync(id, HttpContext.RequestAborted) : null;
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
        AsegurarToken();
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
        if (ProductoNuevoId.HasValue && await productos.ObtenerPorIdAsync(ProductoNuevoId.Value) is { } nuevo)
            AgregarProducto(nuevo, 1);
        await CargarSeguroAsync();
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

        ProductoDto? producto;
        try
        {
            producto = ProductoId > 0 ? await productos.ObtenerPorIdAsync(ProductoId) : null;
        }
        catch (HttpRequestException)
        {
            ModelState.AddModelError(string.Empty, "No se pudo consultar el producto. Intenta nuevamente.");
            await CargarSeguroAsync();
            return Page();
        }

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
        if (!datosValidos) { await CargarSeguroAsync(); return Page(); }
        Formulario.Lineas.RemoveAll(x => x.ProductoId == producto!.Id);
        Formulario.Lineas.Add(new() { ProductoId = producto!.Id, Cantidad = Cantidad, Precio = producto.Precio });
        TempData["OrdenMensaje"] = $"{producto.Nombre} se añadió a la orden.";
        return PersistirYVolver();
    }

    public async Task<IActionResult> OnPostActualizarCantidadAsync(int productoId, int cantidad)
    {
        RestaurarContextoCliente();
        var linea = Formulario.Lineas.FirstOrDefault(x => x.ProductoId == productoId);

        ProductoDto? producto;
        try
        {
            producto = await productos.ObtenerPorIdAsync(productoId);
        }
        catch (HttpRequestException)
        {
            ModelState.AddModelError(string.Empty, "No se pudo consultar el producto. Intenta nuevamente.");
            await CargarSeguroAsync();
            return Page();
        }

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
        if (!cantidadValida) { await CargarSeguroAsync(); return Page(); }
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

    public async Task<IActionResult> OnPostGuardarAsync()
    {
        RestaurarContextoCliente();
        await CargarSeguroAsync();
        if (Formulario.ServicioIds.Count == 0)
            ModelState.AddModelError("Formulario.ServicioIds", "Agrega al menos un servicio a la orden.");
        if (!tokens.EsValido(Formulario.Token))
        {
            Formulario.Token = tokens.Generar();
            ModelState.AddModelError(string.Empty, "La sesión del formulario venció. Revisa los datos y confirma de nuevo.");
        }
        if (!ModelState.IsValid) return Page();

        var solicitud = new CrearOrdenDto(Formulario.VehiculoId, Formulario.MecanicoId, FechaOrden, Formulario.Token,
            Formulario.Lineas.Select(x => new LineaOrdenDto(x.ProductoId, x.Cantidad)).ToArray(),
            Formulario.ServicioIds[0], Formulario.ServicioIds.ToArray(),
            ServiciosSeleccionados.Select(servicio => new LineaServicioOrdenDto(servicio.Id, servicio.Cantidad)).ToArray());

        ResultadoOrdenDto resultado;
        try
        {
            resultado = await ordenes.CrearAsync(solicitud);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (HttpRequestException)
        {
            ModelState.AddModelError(string.Empty, "No se pudo guardar la orden. Intenta nuevamente.");
            return Page();
        }

        if (!resultado.Exito)
        {
            foreach (var error in resultado.Errores) ModelState.AddModelError(MapearError(error.Key), error.Value);
            await CargarSeguroAsync();
            return Page();
        }
        borradores.Eliminar(BorradorId ?? Guid.Empty);
        TempData["OrdenMensaje"] = resultado.EsReenvio
            ? $"La orden ya se había guardado. Total: Bs. {resultado.Total:0.00}."
            : $"Orden guardada correctamente. Total: Bs. {resultado.Total:0.00}.";
        TempData["OrdenCreadaId"] = resultado.OrdenId;
        return RedirectToPage("./Index");
    }

    public IActionResult OnPostNuevoVehiculo()
    {
        AsegurarToken();
        Guid id = borradores.Guardar(Formulario);
        return RedirectToPage("/Vehiculos/Index", new { OrdenBorradorId = id, ClientePreseleccionadoId = Formulario.ClienteNuevoId });
    }

    public IActionResult OnPostNuevoCliente()
    {
        AsegurarToken();
        Guid id = borradores.Guardar(Formulario);
        return RedirectToPage("/Clientes/Index", new { OrdenBorradorId = id });
    }

    public IActionResult OnPostNuevoProducto()
    {
        AsegurarToken();
        Guid id = borradores.Guardar(Formulario);
        return RedirectToPage("/Productos/Index", new { OrdenBorradorId = id });
    }

    public IActionResult OnPostNuevoMecanico()
    {
        if (!User.IsInRole(Roles.Administrador)) return Forbid();
        AsegurarToken();
        Guid id = borradores.Guardar(Formulario);
        return RedirectToPage("/Mecanicos/Index", new { OrdenBorradorId = id });
    }

    private IActionResult PersistirYVolver()
    {
        AsegurarToken();
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

    private async Task CargarSeguroAsync()
    {
        try
        {
            await CargarAsync();
        }
        catch (HttpRequestException)
        {
            ModelState.AddModelError(string.Empty, "No se pudieron cargar los datos del taller. Intenta nuevamente.");
        }
    }

    private async Task CargarAsync()
    {
        FechaOrden = await ordenes.ObtenerFechaActualAsync(HttpContext.RequestAborted);
        VehiculoSeleccionado = Formulario.VehiculoId > 0
            ? await vehiculos.ObtenerPorIdAsync(Formulario.VehiculoId, HttpContext.RequestAborted)
            : null;
        if (VehiculoSeleccionado is not null) BuscarVehiculo = VehiculoSeleccionado.Placa;
        MecanicoSeleccionado = Formulario.MecanicoId > 0
            ? await mecanicos.ObtenerPorIdAsync(Formulario.MecanicoId, HttpContext.RequestAborted)
            : null;
        if (MecanicoSeleccionado is not null)
            TextoMecanico = NombreCompleto(MecanicoSeleccionado);

        var catalogo = await servicios.ObtenerCatalogoAsync(HttpContext.RequestAborted);
        ServicioSeleccionado = Formulario.ServicioId.HasValue
            ? catalogo.FirstOrDefault(servicio => servicio.Id == Formulario.ServicioId.Value)
            : null;
        if (ServicioSeleccionado is not null && string.IsNullOrWhiteSpace(TextoServicio))
            TextoServicio = ServicioSeleccionado.Nombre;

        var serviciosSeleccionados = new List<ServicioSeleccionadoOrdenViewModel>();
        foreach (int servicioId in Formulario.ServicioIds.Distinct())
        {
            ServicioDto? servicio = catalogo.FirstOrDefault(actual => actual.Id == servicioId);
            if (servicio is null) continue;
            serviciosSeleccionados.Add(new ServicioSeleccionadoOrdenViewModel
            {
                Id = servicio.Id,
                Nombre = servicio.Nombre,
                Descripcion = servicio.Descripcion,
                Costo = servicio.Costo,
                TiempoEstimadoHoras = servicio.TiempoEstimadoHoras,
                UnidadCantidad = servicio.UnidadCantidad,
                Cantidad = Formulario.CantidadesServicios.GetValueOrDefault(servicio.Id, 1)
            });
        }
        ServiciosSeleccionados = serviciosSeleccionados;

        ProductoSeleccionado = ProductoId > 0
            ? await productos.ObtenerPorIdAsync(ProductoId, HttpContext.RequestAborted)
            : null;
        if (ProductoSeleccionado is not null) BuscarProducto = ProductoSeleccionado.Nombre;

        var lineas = new List<LineaOrdenTemporalViewModel>();
        foreach (var linea in Formulario.Lineas)
        {
            ProductoDto? producto = await productos.ObtenerPorIdAsync(linea.ProductoId, HttpContext.RequestAborted);
            if (producto is null) continue;
            linea.Precio = producto.Precio;
            lineas.Add(new LineaOrdenTemporalViewModel
            {
                ProductoId = producto.Id,
                Codigo = producto.Codigo,
                Nombre = producto.Nombre,
                Cantidad = linea.Cantidad,
                Stock = producto.Stock,
                Precio = producto.Precio
            });
        }
        Lineas = lineas;

        NombreClienteNuevo = Formulario.ClienteNuevoId.HasValue
            ? await NombreClienteAsync(Formulario.ClienteNuevoId.Value)
            : null;
    }

    private async Task<string?> NombreClienteAsync(int id)
    {
        ClienteDto? cliente = await clientes.ObtenerPorIdAsync(id, HttpContext.RequestAborted);
        return cliente is null
            ? null
            : $"{cliente.PrimerApellido} {cliente.SegundoApellido} {cliente.Nombres}".Trim();
    }

    private void AsegurarToken()
    {
        if (Formulario.Token == Guid.Empty || !tokens.EsValido(Formulario.Token))
            Formulario.Token = tokens.Generar();
    }

    private static string NombreCompleto(MecanicoDto mecanico) =>
        string.Join(" ", new[] { mecanico.Nombres, mecanico.PrimerApellido, mecanico.SegundoApellido }
            .Where(nombre => !string.IsNullOrWhiteSpace(nombre)));

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
