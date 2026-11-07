namespace TallerMecanico.Aplicacion.Errores;

public sealed class RegistroDuplicadoException(Exception causa)
    : Exception("Ya existe un registro con el mismo identificador único.", causa);
