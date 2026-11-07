namespace TallerMecanico.Dominio.Entidades;

public sealed record ServicioCatalogo(string Codigo, string Nombre, decimal PrecioInicial, string? UnidadCantidad);

public static class CatalogoServicios
{
    public static IReadOnlyList<ServicioCatalogo> Servicios { get; } = Array.AsReadOnly<ServicioCatalogo>([
        new("CAMBIO_ACEITE", "Cambio de aceite", 250m, null),
        new("CAMBIO_FILTROS", "Cambio de filtros", 100m, "unidad"),
        new("CAMBIO_PASTILLAS_FRENO", "Cambio de pastillas de freno", 250m, "unidad"),
        new("DIAGNOSTICO_ESCANER", "Diagnóstico con escáner", 120m, null),
        new("REPARACION_MOTOR", "Reparación de motor", 1500m, null),
        new("CAMBIO_BUJIAS", "Cambio de bujías", 180m, "unidad"),
        new("REPARACION_SUSPENSION", "Reparación de suspensión", 400m, "unidad"),
        new("CAMBIO_BATERIA", "Cambio de batería", 550m, null),
        new("CAMBIO_EMBRAGUE", "Cambio de embrague", 1000m, null),
        new("ALINEACION_BALANCEO", "Alineación y balanceo", 180m, null)
    ]);

    public static ServicioCatalogo? Obtener(string? codigo) =>
        Servicios.SingleOrDefault(servicio => servicio.Codigo == codigo);

    public static bool EsSeleccionable(Servicio servicio) =>
        Obtener(servicio.CodigoCatalogo) is { } definicion &&
        servicio.UnidadCantidad == definicion.UnidadCantidad;
}
