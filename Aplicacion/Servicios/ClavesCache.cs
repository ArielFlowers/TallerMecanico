namespace TallerMecanico.Aplicacion.Servicios;

// H2: claves conocidas de la caché distribuida.
// Inspección: docker exec -it taller-mecanico-redis redis-cli GET autotaller:dashboard:resumen
public static class ClavesCache
{
    public const string DashboardResumen = "autotaller:dashboard:resumen";
}
