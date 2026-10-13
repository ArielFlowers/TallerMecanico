using MySqlConnector;
using TallerMecanico.Application.Ports;
using TallerMecanico.Models;
using TallerMecanico.Validators;
using TallerMecanico.ViewModels;

namespace TallerMecanico.Services;

public class ProductoService
{
    private const string MensajeCodigoDuplicado =
        "Ya existe un producto registrado con este código.";

    private const string MensajeProductoNoExiste =
        "El producto solicitado no existe.";

    private readonly IProductoPort _productoPort;
    private readonly ValidacionProductos _validacionProductos;

    private readonly ICurrentUser _currentUser;
    public ProductoService(
        IProductoPort productoPort,
        ValidacionProductos validacionProductos,
        ICurrentUser currentUser)
    {
        _productoPort = productoPort;
        _validacionProductos = validacionProductos;
        _currentUser = currentUser;
    }

    public IReadOnlyList<Producto> Obtener()
    {
        return _productoPort.GetAll();
    }

    public IReadOnlyList<Producto> Buscar(
        string terminoBusqueda)
    {
        if (string.IsNullOrWhiteSpace(
                terminoBusqueda))
        {
            return Obtener();
        }

        return _productoPort.Search(
            terminoBusqueda.Trim());
    }

    public Producto? ObtenerPorId(
        int id)
    {
        if (id <= 0)
        {
            return null;
        }

        return _productoPort.GetById(
            id);
    }

    public (
        ProductoFormViewModel Formulario,
        IReadOnlyDictionary<string, string> Errores)
        Crear(
            ProductoFormViewModel formulario)
    {
        ProductoFormViewModel normalizado =
            _validacionProductos.Normalizar(
                formulario);

        var errores =
            new Dictionary<string, string>(
                _validacionProductos.Validar(
                    normalizado));

        ValidarCodigoDuplicado(
            normalizado,
            0,
            errores);

        if (errores.Count > 0)
        {
            return (
                normalizado,
                errores);
        }

        Producto producto =
            CrearProducto(
                normalizado);

        try
        {
            _productoPort.Add(
                producto);
        }
        catch (MySqlException exception)
            when (exception.Number == 1062)
        {
            errores[
                nameof(
                    ProductoFormViewModel.Codigo)] =
                MensajeCodigoDuplicado;
        }

        return (
            normalizado,
            errores);
    }

    public (
        ProductoFormViewModel Formulario,
        IReadOnlyDictionary<string, string> Errores)
        Actualizar(
            ProductoFormViewModel formulario)
    {
        ProductoFormViewModel normalizado =
            _validacionProductos.Normalizar(
                formulario);

        var errores =
            new Dictionary<string, string>(
                _validacionProductos.Validar(
                    normalizado));

        if (normalizado.Id <= 0)
        {
            errores[string.Empty] =
                MensajeProductoNoExiste;

            return (
                normalizado,
                errores);
        }

        Producto? existente =
            _productoPort.GetById(
                normalizado.Id);

        if (existente is null)
        {
            errores[string.Empty] =
                MensajeProductoNoExiste;

            return (
                normalizado,
                errores);
        }

        ValidarCodigoDuplicado(
            normalizado,
            normalizado.Id,
            errores);

        if (errores.Count > 0)
        {
            return (
                normalizado,
                errores);
        }

        Producto producto =
            new()
            {
                Id =
                    normalizado.Id,

                Codigo =
                    normalizado.Codigo ??
                    string.Empty,

                Nombre =
                    normalizado.Nombre ??
                    string.Empty,

                Precio =
                    normalizado.Precio ??
                    0,

                Stock =
                    normalizado.Stock ??
                    0,

                StockMinimo =
                    normalizado.StockMinimo ??
                    0,

                CreadoPor =
                    existente.CreadoPor,

                FechaCreacion =
                    existente.FechaCreacion
            };

        try
        {
            _productoPort.Update(
                producto);
        }
        catch (MySqlException exception)
            when (exception.Number == 1062)
        {
            errores[
                nameof(
                    ProductoFormViewModel.Codigo)] =
                MensajeCodigoDuplicado;
        }

        return (
            normalizado,
            errores);
    }

    public string? Eliminar(
        int id)
    {
        if (id <= 0)
        {
            return MensajeProductoNoExiste;
        }

        Producto? producto =
            _productoPort.GetById(
                id);

        if (producto is null)
        {
            return MensajeProductoNoExiste;
        }

        try { _productoPort.Delete(id); }
        catch (MySqlException error) when (error.Number == 1451)
        { return "No se puede eliminar el producto porque tiene órdenes asociadas."; }

        return null;
    }

    public bool DescontarStock(
        int productoId,
        int cantidad)
    {
        if (productoId <= 0 ||
            cantidad <= 0)
        {
            return false;
        }

        return _productoPort.Descontar(
            productoId,
            cantidad);
    }

    public void RestituirStock(
        int productoId,
        int cantidad)
    {
        if (productoId <= 0 ||
            cantidad <= 0)
        {
            return;
        }

        _productoPort.Restituir(
            productoId,
            cantidad);
    }

    private void ValidarCodigoDuplicado(
        ProductoFormViewModel formulario,
        int idExcluido,
        IDictionary<string, string> errores)
    {
        if (errores.ContainsKey(
                nameof(
                    ProductoFormViewModel.Codigo)))
        {
            return;
        }

        if (_productoPort.ExistsByCodigo(
                formulario.Codigo ??
                string.Empty,
                idExcluido))
        {
            errores[
                nameof(
                    ProductoFormViewModel.Codigo)] =
                MensajeCodigoDuplicado;
        }
    }

    private Producto CrearProducto(
        ProductoFormViewModel formulario)
    {
        return new Producto
        {
            Codigo =
                formulario.Codigo ??
                string.Empty,

            Nombre =
                formulario.Nombre ??
                string.Empty,

            Precio =
                formulario.Precio ??
                0,

            Stock =
                formulario.Stock ??
                0,

            StockMinimo =
                formulario.StockMinimo ??
                0,
            CreadoPor = _currentUser.Username
                 ?? throw new UnauthorizedAccessException(
                     "Se requiere un usuario autenticado."),

            FechaCreacion =
                DateTime.Now
        };
    }
}
