namespace TallerMecanico.Aplicacion.Dto;

public sealed record SugerenciaProducto(int Id, string TextoPrincipal, string TextoSecundario)
{
    public int Stock { get; init; }
}
