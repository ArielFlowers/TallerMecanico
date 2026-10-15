using TallerMecanico.Dominio.Entidades;

namespace TallerMecanico.Aplicacion.CasosUso.Ordenes;

public sealed record OrdenResumen(
    int Id,
    DateTime Fecha,
    string Placa,
    string? NombreCliente,
    string NombreMecanico,
    string? NombreServicio,
    decimal Total,
    EstadoOrden Estado,
    string CreadoPor);
