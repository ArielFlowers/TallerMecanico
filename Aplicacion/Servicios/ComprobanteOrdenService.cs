using TallerMecanico.Application.Ports;

namespace TallerMecanico.Services;

public sealed class ComprobanteOrdenService(
    ConsultaOrdenesService consultas,
    IComprobanteOrdenPdf generador,
    ImporteEnPalabras importeEnPalabras,
    ICurrentUser usuario,
    TimeProvider reloj)
{
    public byte[]? Generar(int id)
    {
        var orden = consultas.ObtenerDetalle(id);
        if (orden is null) return null;

        return generador.Generar(orden, importeEnPalabras.Convertir(orden.Total),
            usuario.Username!, reloj.GetUtcNow());
    }
}
