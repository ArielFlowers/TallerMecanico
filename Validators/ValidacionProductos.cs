using System.Text.RegularExpressions;
using TallerMecanico.ViewModels;

namespace TallerMecanico.Validators;

public class ValidacionProductos
{
    public const int LongitudMaximaCodigo = 50;
    public const int LongitudMaximaNombre = 100;

    public ProductoFormViewModel Normalizar(
        ProductoFormViewModel formulario)
    {
        return new ProductoFormViewModel
        {
            Id = formulario.Id,

            Codigo =
                (formulario.Codigo ?? string.Empty)
                .Trim()
                .ToUpperInvariant(),

            Nombre =
                NormalizarTexto(
                    formulario.Nombre),

            Precio =
                formulario.Precio,

            Stock =
                formulario.Stock,

            StockMinimo =
                formulario.StockMinimo
        };
    }

    public IReadOnlyDictionary<string, string> Validar(
        ProductoFormViewModel formulario)
    {
        var errores =
            new Dictionary<string, string>();

        ValidarCodigo(
            formulario,
            errores);

        ValidarNombre(
            formulario,
            errores);

        ValidarPrecio(
            formulario,
            errores);

        ValidarStock(
            formulario,
            errores);

        ValidarStockMinimo(
            formulario,
            errores);

        return errores;
    }

    private static void ValidarCodigo(
        ProductoFormViewModel formulario,
        IDictionary<string, string> errores)
    {
        if (string.IsNullOrWhiteSpace(
                formulario.Codigo))
        {
            errores[
                nameof(
                    ProductoFormViewModel.Codigo)] =
                "El código es obligatorio.";

            return;
        }

        if (formulario.Codigo.Length >
            LongitudMaximaCodigo)
        {
            errores[
                nameof(
                    ProductoFormViewModel.Codigo)] =
                $"El código no puede superar los {LongitudMaximaCodigo} caracteres.";
        }
    }

    private static void ValidarNombre(
        ProductoFormViewModel formulario,
        IDictionary<string, string> errores)
    {
        if (string.IsNullOrWhiteSpace(
                formulario.Nombre))
        {
            errores[
                nameof(
                    ProductoFormViewModel.Nombre)] =
                "El nombre es obligatorio.";

            return;
        }

        if (formulario.Nombre.Length >
            LongitudMaximaNombre)
        {
            errores[
                nameof(
                    ProductoFormViewModel.Nombre)] =
                $"El nombre no puede superar los {LongitudMaximaNombre} caracteres.";
        }
    }

    private static void ValidarPrecio(
        ProductoFormViewModel formulario,
        IDictionary<string, string> errores)
    {
        if (!formulario.Precio.HasValue)
        {
            errores[
                nameof(
                    ProductoFormViewModel.Precio)] =
                "El precio es obligatorio.";

            return;
        }

        if (formulario.Precio.Value <= 0)
        {
            errores[
                nameof(
                    ProductoFormViewModel.Precio)] =
                "El precio debe ser mayor a cero.";
        }
    }

    private static void ValidarStock(
        ProductoFormViewModel formulario,
        IDictionary<string, string> errores)
    {
        if (!formulario.Stock.HasValue)
        {
            errores[
                nameof(
                    ProductoFormViewModel.Stock)] =
                "El stock es obligatorio.";

            return;
        }

        if (formulario.Stock.Value < 0)
        {
            errores[
                nameof(
                    ProductoFormViewModel.Stock)] =
                "El stock no puede ser negativo.";
        }
    }

    private static void ValidarStockMinimo(
        ProductoFormViewModel formulario,
        IDictionary<string, string> errores)
    {
        if (!formulario.StockMinimo.HasValue)
        {
            errores[
                nameof(
                    ProductoFormViewModel.StockMinimo)] =
                "El stock mínimo es obligatorio.";

            return;
        }

        if (formulario.StockMinimo.Value < 0)
        {
            errores[
                nameof(
                    ProductoFormViewModel.StockMinimo)] =
                "El stock mínimo no puede ser negativo.";
        }
    }

    private static string NormalizarTexto(
        string? valor)
    {
        return Regex.Replace(
            (valor ?? string.Empty).Trim(),
            @"\s+",
            " ");
    }
}