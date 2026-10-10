namespace TallerMecanico.Application.Ordenes;

public class PersistenciaOrdenException(string mensaje, Exception? causa = null) : Exception(mensaje, causa);

public sealed class TokenOrdenDuplicadoException(Exception causa)
    : PersistenciaOrdenException("El token de la orden ya fue registrado.", causa);
