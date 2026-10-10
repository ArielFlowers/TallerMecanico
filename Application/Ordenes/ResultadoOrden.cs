using TallerMecanico.Models;

namespace TallerMecanico.Application.Ordenes;

public sealed record ResultadoOrden(
    bool Exito,
    int? OrdenId,
    decimal Total,
    EstadoOrden? Estado,
    bool EsReenvio,
    IReadOnlyDictionary<string, string> Errores)
{
    public static ResultadoOrden Correcto(Orden orden, bool esReenvio = false) =>
        new(true, orden.Id, orden.Total, orden.Estado, esReenvio, new Dictionary<string, string>());

    public static ResultadoOrden Fallo(IReadOnlyDictionary<string, string> errores) =>
        new(false, null, 0, null, false, errores);

    public static ResultadoOrden Fallo(string campo, string mensaje) =>
        Fallo(new Dictionary<string, string> { [campo] = mensaje });
}
