using TallerMecanico.Aplicacion.DTOs;

namespace TallerMecanico.Aplicacion.Puertos;

public interface IUsuarioConsultaPort
{
    IReadOnlyList<UsuarioListadoDto> Listar(int pagina, int tamanoPagina);

    int ContarTotal();
}