using TallerMecanico.Aplicacion.Dto;
using TallerMecanico.Aplicacion.Puertos;

namespace TallerMecanico.Infraestructura.Cache;

// H2: se usa cuando no hay REDIS_HOST configurado (por ejemplo,
// dotnet run sin Docker o pruebas). No simula una lectura a Redis.
public sealed class CacheDistribuidaDeshabilitada : ICacheDistribuidaPort
{
    public bool EstaDisponible => false;

    public ResultadoLecturaCache Obtener(string clave) =>
        new(EstadoLecturaCache.Deshabilitado);

    public bool Guardar(string clave, string valor, TimeSpan expiracion) => false;

    public ResultadoEliminacionCache Eliminar(string clave) => ResultadoEliminacionCache.Deshabilitado;

    public TimeSpan? TiempoRestante(string clave) => null;
}
