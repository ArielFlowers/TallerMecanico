using TallerMecanico.Aplicacion.DTOs;

namespace TallerMecanico.Aplicacion.Puertos;

public interface IUsuarioConsultaPort
{
    IReadOnlyList<UsuarioListadoDto> Listar(int pagina, int tamanoPagina);

    int ContarTotal();

    IReadOnlyList<UsuarioListadoDto> ListarFiltrados(
        string? busqueda, string? rol, bool? activo,
        int pagina, int tamanoPagina)
        => throw new NotSupportedException("Listado filtrado no implementado.");

    int ContarFiltrados(string? busqueda, string? rol, bool? activo)
        => throw new NotSupportedException("Conteo filtrado no implementado.");

    int ContarUsuariosActivos()
        => throw new NotSupportedException("Conteo de activos no implementado.");

    int ContarAdministradoresActivosListado()
        => throw new NotSupportedException("Conteo de administradores no implementado.");
}