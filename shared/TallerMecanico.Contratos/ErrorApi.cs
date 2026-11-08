namespace TallerMecanico.Contratos;

// Cuerpo de las respuestas de error de todas las APIs.
// Campo indica qué dato del formulario falló y Codigo distingue casos que la página trata aparte.
public sealed record ErrorApi(string Mensaje, string? Campo = null, string? Codigo = null);
