using TallerMecanico.Dominio.Entidades;

namespace TallerMecanico.Aplicacion.CasosUso.Ordenes;

public sealed record OrdenResumen(
    int Id,
    DateTime Fecha,
    string Placa,
    string? NombreCliente,
    string NombreMecanico,
    decimal Total,
    EstadoOrden Estado,
    string CreadoPor);
