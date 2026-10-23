namespace TallerMecanico.Pages.Shared;

public sealed class BuscadorAutocompletadoModel
{
    public const string ModoFiltrar = "filtrar";
    public const string ModoSeleccionar = "seleccionar";
    public const int MinimoCaracteres = 2;
    public const int EsperaMilisegundos = 300;
    public const int MaximoSugerencias = 10;

    public required string Id { get; init; }
    public required string UrlSugerencias { get; init; }
    public string Modo { get; init; } = ModoFiltrar;
    public string Etiqueta { get; init; } = "Buscar";
    public string Placeholder { get; init; } = "Escribe para buscar";
    public string NombreCampoTexto { get; init; } = "TerminoBusqueda";
    public string? NombreCampoId { get; init; }
    public int UmbralMinimoCaracteres { get; init; } = MinimoCaracteres;
    public int LimiteSugerencias { get; init; } = MaximoSugerencias;
    public bool MostrarAlEnfocar { get; init; }
    public string Texto { get; init; } = string.Empty;
    public int? IdSeleccionado { get; init; }
    public string MensajeInicial { get; init; } = "Escribe al menos 2 caracteres.";
    public string MensajeCargando { get; init; } = "Buscando…";
    public string MensajeSinResultados { get; init; } = "No se encontraron coincidencias.";
    public string MensajeResultados { get; init; } = "{cantidad} sugerencias disponibles. Usa las flechas y Enter para seleccionar.";
    public string MensajeSeleccionado { get; init; } = "Registro seleccionado.";
    public string MensajeError { get; init; } = "No se pudo completar la búsqueda. Intenta nuevamente.";
    public string MensajeSesion { get; init; } = "Tu sesión venció o no tienes permiso para realizar esta búsqueda.";
}
