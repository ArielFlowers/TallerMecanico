namespace TallerMecanico.Aplicacion.Errores;

public sealed class ReferenciaInexistenteException(Exception causa)
    : Exception("El registro relacionado no existe.", causa);
