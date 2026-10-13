using TallerMecanico.Models;

namespace TallerMecanico.Application.Ordenes;

public sealed record OrdenResumen(
    int Id,
    DateTime Fecha,
    string Placa,
    string? NombreCliente,
    string NombreMecanico,
    decimal Total,
    EstadoOrden Estado,
    string CreadoPor);
