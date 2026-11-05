namespace TallerMecanico.Contratos.Catalogos;

// Reglas que usan por igual el formulario del Frontend y la validación del servicio.
public static class ReglasVehiculo
{
    public const string PatronPlaca = "^[0-9]{3,4}[A-Z]{3}$";

    public const string MensajeFormatoPlaca =
        "La placa debe contener 3 o 4 números y 3 letras. Ejemplos: 123ABC o 1234ABC.";

    public const string PatronPlacaExtranjera = "^[A-Z0-9]{2,15}$";

    public const string MensajeFormatoPlacaExtranjera =
        "La placa extranjera debe contener entre 2 y 15 letras o números, en cualquier orden.";

    public const int LongitudMaximaObservaciones = 250;
}
