namespace TallerMecanico.Aplicacion.Puertos;

// H2: puerto de caché distribuida compartida por todas las réplicas.
// La implementación real usa Redis; si Redis no está configurado o no
// responde, las operaciones degradan sin lanzar excepciones y la
// aplicación continúa consultando MySQL.
public interface ICacheDistribuidaPort
{
    bool EstaDisponible { get; }

    string? Obtener(string clave);

    bool Guardar(string clave, string valor, TimeSpan expiracion);

    bool Eliminar(string clave);

    TimeSpan? TiempoRestante(string clave);
}
