using System.Text.RegularExpressions;
using TallerMecanico.Dominio.Entidades;
using TallerMecanico.Contratos.Catalogos;

namespace TallerMecanico.Aplicacion.Validaciones;

public class ValidacionVehiculos
{
    public const string PatronPlaca = ReglasVehiculo.PatronPlaca;

    public const string MensajeFormatoPlaca = ReglasVehiculo.MensajeFormatoPlaca;

    public const string PatronPlacaExtranjera = ReglasVehiculo.PatronPlacaExtranjera;

    public const string MensajeFormatoPlacaExtranjera = ReglasVehiculo.MensajeFormatoPlacaExtranjera;

    public const int LongitudMaximaModelo =
        60;

    public const int LongitudMaximaObservaciones = ReglasVehiculo.LongitudMaximaObservaciones;

    public VehiculoFormViewModel Normalizar(
        VehiculoFormViewModel formulario)
    {
        string marca =
            BuscarNombreCanonico(
                formulario.Marca,
                CatalogoVehiculos.Marcas.Keys);

        var modelos =
            CatalogoVehiculos.Marcas.TryGetValue(
                marca,
                out var disponibles)
                ? disponibles
                : Array.Empty<string>();

        return new VehiculoFormViewModel
        {
            Id =
                formulario.Id,

            Placa =
                NormalizarPlaca(formulario.Placa, formulario.EsPlacaExtranjera),

            EsPlacaExtranjera = formulario.EsPlacaExtranjera,

            Marca =
                marca,

            Modelo =
                BuscarNombreCanonico(
                    formulario.Modelo,
                    modelos),

            Kilometraje =
                formulario.Kilometraje ?? 0,

            Observaciones =
                Regex.Replace(
                    (formulario.Observaciones ??
                     string.Empty).Trim(),
                    @"[^\S\r\n]+",
                    " "),

            ClienteId =
                formulario.ClienteId
        };
    }

    public IReadOnlyDictionary<string, string> Validar(
        VehiculoFormViewModel formulario)
    {
        var errores =
            new Dictionary<string, string>();

        ValidarPlaca(
            formulario,
            errores);

        ValidarMarcaModelo(
            formulario,
            errores);

        ValidarKilometraje(
            formulario,
            errores);

        ValidarObservaciones(
            formulario,
            errores);

        ValidarCliente(
            formulario,
            errores);

        return errores;
    }

    private static void ValidarPlaca(
        VehiculoFormViewModel formulario,
        IDictionary<string, string> errores)
    {
        if (string.IsNullOrWhiteSpace(
                formulario.Placa))
        {
            errores[
                nameof(formulario.Placa)] =
                "La placa es obligatoria.";

            return;
        }

        string patron = formulario.EsPlacaExtranjera
            ? PatronPlacaExtranjera
            : PatronPlaca;

        if (!Regex.IsMatch(formulario.Placa, patron))
        {
            errores[
                nameof(formulario.Placa)] =
                formulario.EsPlacaExtranjera
                    ? MensajeFormatoPlacaExtranjera
                    : MensajeFormatoPlaca;
        }
    }

    private static void ValidarMarcaModelo(
        VehiculoFormViewModel formulario,
        IDictionary<string, string> errores)
    {
        if (!CatalogoVehiculos.Marcas.TryGetValue(
                formulario.Marca ??
                string.Empty,
                out var modelos))
        {
            errores[
                nameof(formulario.Marca)] =
                "Selecciona una marca válida.";
        }

        if (string.IsNullOrWhiteSpace(
                formulario.Modelo))
        {
            errores[
                nameof(formulario.Modelo)] =
                "El modelo es obligatorio.";

            return;
        }

        if (formulario.Modelo.Length >
            LongitudMaximaModelo)
        {
            errores[
                nameof(formulario.Modelo)] =
                $"El modelo no puede superar los {LongitudMaximaModelo} caracteres.";

            return;
        }

        if (modelos is null ||
            !modelos.Contains(
                formulario.Modelo))
        {
            errores[
                nameof(formulario.Modelo)] =
                "Selecciona un modelo correspondiente a la marca.";
        }
    }

    private static void ValidarKilometraje(
        VehiculoFormViewModel formulario,
        IDictionary<string, string> errores)
    {
        if (formulario.Kilometraje < 0)
        {
            errores[
                nameof(formulario.Kilometraje)] =
                "El kilometraje no puede ser negativo.";
        }
    }

    private static void ValidarObservaciones(
        VehiculoFormViewModel formulario,
        IDictionary<string, string> errores)
    {
        if (formulario.Observaciones?.Length >
            LongitudMaximaObservaciones)
        {
            errores[
                nameof(formulario.Observaciones)] =
                $"Las observaciones no pueden superar los {LongitudMaximaObservaciones} caracteres.";
        }
    }

    private static void ValidarCliente(
        VehiculoFormViewModel formulario,
        IDictionary<string, string> errores)
    {
        if (!formulario.ClienteId.HasValue)
        {
            errores[nameof(formulario.ClienteId)] =
                "Selecciona un cliente. Si todavía no existe, regístralo antes de guardar el vehículo.";
            return;
        }

        if (formulario.ClienteId.Value <= 0)
        {
            errores[
                nameof(formulario.ClienteId)] =
                "Selecciona un cliente válido.";
        }
    }

    private static string BuscarNombreCanonico(
        string? valor,
        IEnumerable<string> opciones)
    {
        string clave =
            QuitarEspacios(valor);

        return opciones.FirstOrDefault(
            opcion =>
                string.Equals(
                    QuitarEspacios(opcion),
                    clave,
                    StringComparison.OrdinalIgnoreCase))
            ?? (valor ?? string.Empty).Trim();
    }

    private static string QuitarEspacios(
        string? valor)
    {
        return string.Concat(
            (valor ?? string.Empty)
                .Where(
                    caracter =>
                        !char.IsWhiteSpace(caracter)));
    }

    private static string NormalizarPlaca(string? placa, bool extranjera)
    {
        string normalizada = QuitarEspacios(placa).ToUpperInvariant();
        return extranjera ? normalizada.Replace("-", string.Empty) : normalizada;
    }
}
