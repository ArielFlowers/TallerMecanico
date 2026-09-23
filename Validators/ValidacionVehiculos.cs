using System.Text.RegularExpressions;
using TallerMecanico.Models;
using TallerMecanico.ViewModels;

namespace TallerMecanico.Validators;

public class ValidacionVehiculos
{
    public const string PatronPlaca = "^[0-9]{3,4}[A-Z]{3}$";
    public const string MensajeFormatoPlaca = "La placa debe contener 3 o 4 números y 3 letras. Ejemplos: 123ABC o 1234ABC.";
    public const int LongitudMaximaModelo = 60;
    public const int LongitudMaximaObservaciones = 250;

    public VehiculoFormViewModel Normalizar(VehiculoFormViewModel formulario)
    {
        string marca = BuscarNombreCanonico(formulario.Marca, CatalogoVehiculos.Marcas.Keys);
        var modelos = CatalogoVehiculos.Marcas.TryGetValue(marca, out var disponibles)
            ? disponibles : Array.Empty<string>();

        return new VehiculoFormViewModel
        {
            Id = formulario.Id,
            Placa = QuitarEspacios(formulario.Placa).ToUpperInvariant(),
            Marca = marca,
            Modelo = BuscarNombreCanonico(formulario.Modelo, modelos),
            Kilometraje = formulario.Kilometraje ?? 0,
            Observaciones = Regex.Replace(
                (formulario.Observaciones ?? string.Empty).Trim(), @"[^\S\r\n]+", " ")
        };
    }

    public IReadOnlyDictionary<string, string> Validar(VehiculoFormViewModel formulario)
    {
        var errores = new Dictionary<string, string>();

        if (string.IsNullOrWhiteSpace(formulario.Placa))
        {
            errores[nameof(formulario.Placa)] = "La placa es obligatoria.";
        }
        else if (!Regex.IsMatch(formulario.Placa, PatronPlaca) || formulario.Placa.Length is < 6 or > 7)
        {
            errores[nameof(formulario.Placa)] = MensajeFormatoPlaca;
        }

        if (!CatalogoVehiculos.Marcas.TryGetValue(formulario.Marca ?? string.Empty, out var modelos))
        {
            errores[nameof(formulario.Marca)] = "Selecciona una marca válida.";
        }

        if (string.IsNullOrWhiteSpace(formulario.Modelo))
        {
            errores[nameof(formulario.Modelo)] = "El modelo es obligatorio.";
        }
        else if (formulario.Modelo.Length > LongitudMaximaModelo)
        {
            errores[nameof(formulario.Modelo)] = $"El modelo no puede superar los {LongitudMaximaModelo} caracteres.";
        }
        else if (modelos is null || !modelos.Contains(formulario.Modelo))
        {
            errores[nameof(formulario.Modelo)] = "Selecciona un modelo correspondiente a la marca.";
        }

        if (formulario.Kilometraje < 0)
        {
            errores[nameof(formulario.Kilometraje)] = "El kilometraje no puede ser negativo.";
        }

        if (formulario.Observaciones?.Length > LongitudMaximaObservaciones)
        {
            errores[nameof(formulario.Observaciones)] = $"Las observaciones no pueden superar los {LongitudMaximaObservaciones} caracteres.";
        }

        return errores;
    }

    private static string BuscarNombreCanonico(string? valor, IEnumerable<string> opciones)
    {
        string clave = QuitarEspacios(valor);
        return opciones.FirstOrDefault(opcion =>
            string.Equals(QuitarEspacios(opcion), clave, StringComparison.OrdinalIgnoreCase))
            ?? (valor ?? string.Empty).Trim();
    }

    private static string QuitarEspacios(string? valor)
    {
        return string.Concat((valor ?? string.Empty).Where(caracter => !char.IsWhiteSpace(caracter)));
    }
}
