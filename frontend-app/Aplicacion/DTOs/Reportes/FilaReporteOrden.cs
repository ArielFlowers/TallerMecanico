namespace TallerMecanico.Aplicacion.DTOs.Reportes;

public sealed class FilaReporteOrden
{
    public int OrdenId { get; set; }

    public DateTime Fecha { get; set; }

    public string Placa { get; set; } = string.Empty;

    public string? NombreCliente { get; set; }

    public string NombreMecanico { get; set; } = string.Empty;

    public string? NombreServicio { get; set; }

    public decimal Total { get; set; }

    public string Estado { get; set; } = string.Empty;

    public string CreadoPor { get; set; } = string.Empty;
}
