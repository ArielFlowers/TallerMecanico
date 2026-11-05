namespace TallerMecanico.Aplicacion.Errores;

public sealed class RegistroRelacionadoException(Exception causa)
    : Exception("El registro tiene datos relacionados y no puede eliminarse.", causa);
