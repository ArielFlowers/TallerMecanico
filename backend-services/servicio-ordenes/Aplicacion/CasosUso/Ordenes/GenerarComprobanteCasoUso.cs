using TallerMecanico.Aplicacion.CasosUso.Ordenes.Contratos;
using TallerMecanico.Aplicacion.Puertos;
using TallerMecanico.Dominio.Servicios;

namespace TallerMecanico.Aplicacion.CasosUso.Ordenes;

public sealed class GenerarComprobanteCasoUso(
    ConsultarOrdenesCasoUso consultas,
    IComprobantePdf generador,
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
