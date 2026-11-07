namespace TallerMecanico.Contratos.Catalogos;

public sealed class ClienteDto
{
    public int Id { get; set; }
    public string Ci { get; set; } = string.Empty;
    public string ComplementoCi { get; set; } = string.Empty;
    public string Nombres { get; set; } = string.Empty;
    public string PrimerApellido { get; set; } = string.Empty;
    public string SegundoApellido { get; set; } = string.Empty;
    public string Celular { get; set; } = string.Empty;
    public string CreadoPor { get; set; } = string.Empty;
    public DateTime FechaCreacion { get; set; }
}

public sealed class VehiculoDto
{
    public int Id { get; set; }
    public string Placa { get; set; } = string.Empty;
    public bool EsPlacaExtranjera { get; set; }
    public string Marca { get; set; } = string.Empty;
    public string Modelo { get; set; } = string.Empty;
    public int Kilometraje { get; set; }
    public string Observaciones { get; set; } = string.Empty;
    public int? ClienteId { get; set; }
}

public sealed class ProductoDto
{
    public int Id { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public decimal Precio { get; set; }
    public int Stock { get; set; }
    public int StockMinimo { get; set; }
    public string CreadoPor { get; set; } = string.Empty;
    public DateTime FechaCreacion { get; set; }
}

public sealed class MecanicoDto
{
    public int Id { get; set; }
    public string Ci { get; set; } = string.Empty;
    public string ComplementoCi { get; set; } = string.Empty;
    public string Nombres { get; set; } = string.Empty;
    public string PrimerApellido { get; set; } = string.Empty;
    public string SegundoApellido { get; set; } = string.Empty;
    public string Genero { get; set; } = string.Empty;
    public string Especialidad { get; set; } = string.Empty;
    public string Celular { get; set; } = string.Empty;
}

public sealed class ServicioDto
{
    public int Id { get; set; }
    public string? CodigoCatalogo { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
    public decimal Costo { get; set; }
    public decimal TiempoEstimadoHoras { get; set; }
    public string? UnidadCantidad { get; set; }
}

public sealed class HistorialCostoServicioDto
{
    public int Id { get; set; }
    public int ServicioId { get; set; }
    public string NombreServicio { get; set; } = string.Empty;
    public decimal CostoAnterior { get; set; }
    public decimal CostoNuevo { get; set; }
    public DateTime FechaCambio { get; set; }
}

// Respuesta al guardar: el formulario normalizado y sus errores por campo.
public sealed record ResultadoFormulario<TFormulario>(
    TFormulario Formulario,
    IReadOnlyDictionary<string, string> Errores);

public sealed record ResultadoMecanico(
    bool Actualizado,
    IReadOnlyDictionary<string, string> Errores);
