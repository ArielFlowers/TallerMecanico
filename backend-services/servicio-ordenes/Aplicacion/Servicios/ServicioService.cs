using System.Text.RegularExpressions;
using TallerMecanico.Contratos.Catalogos;
using TallerMecanico.Aplicacion.Puertos;
using TallerMecanico.Dominio.Entidades;

namespace TallerMecanico.Aplicacion.Servicios;

public class ServicioService : IServicioService
{
    private const int MinimoCaracteresSugerencias = 2;
    private const int MaximoCaracteresSugerencias = 100;

    private readonly IRepository<Servicio> _servicioRepository;
    private readonly IConsultaServiciosPort _consultaServicios;
    private readonly ICurrentUser? _currentUser;
    private readonly IAuditoriaPort? _auditoria;

    public ServicioService(
        IRepository<Servicio> servicioRepository,
        IConsultaServiciosPort consultaServicios,
        ICurrentUser? currentUser = null,
        IAuditoriaPort? auditoria = null)
    {
        _auditoria = auditoria;
        _servicioRepository = servicioRepository;
        _consultaServicios = consultaServicios;
        _currentUser = currentUser;
    }

    public List<Servicio> ObtenerTodos()
    {
        return _servicioRepository.GetAll();
    }

    public List<Servicio> ObtenerCatalogo() => _consultaServicios.ObtenerCatalogo();

    public Servicio? ObtenerDelCatalogo(int id) => _consultaServicios.ObtenerDelCatalogo(id);

    public Task<IReadOnlyList<SugerenciaServicio>> SugerirCatalogoAsync(
        string? termino, CancellationToken cancellationToken = default) =>
        SugerirAsync(termino, true, cancellationToken);

    public Servicio? ObtenerPorId(int id)
    {
        return _servicioRepository.GetById(id);
    }

    public Task<IReadOnlyList<Servicio>> BuscarAsync(
        string? termino, CancellationToken cancellationToken = default)
    {
        return _consultaServicios.BuscarAsync(termino?.Trim() ?? string.Empty, cancellationToken);
    }

    public Task<IReadOnlyList<SugerenciaServicio>> SugerirAsync(
        string? termino, CancellationToken cancellationToken = default) =>
        SugerirAsync(termino, false, cancellationToken);

    private Task<IReadOnlyList<SugerenciaServicio>> SugerirAsync(
        string? termino, bool soloCatalogo, CancellationToken cancellationToken)
    {
        var terminoNormalizado = Regex.Replace(termino?.Trim() ?? string.Empty, @"\s+", " ");
        if (terminoNormalizado.Length < MinimoCaracteresSugerencias ||
            terminoNormalizado.Length > MaximoCaracteresSugerencias)
        {
            return Task.FromResult<IReadOnlyList<SugerenciaServicio>>([]);
        }

        return soloCatalogo
            ? _consultaServicios.SugerirCatalogoAsync(terminoNormalizado, cancellationToken)
            : _consultaServicios.SugerirAsync(terminoNormalizado, cancellationToken);
    }

    public void Crear(Servicio servicio)
    {
        ValidarServicio(servicio);

        servicio.CreadoPor = UsuarioActual();
        servicio.FechaCreacion = DateTime.Now;

        _servicioRepository.Add(servicio);
    }

    public void Actualizar(Servicio servicio)
    {
        ValidarServicio(servicio);

        Servicio? existente = _servicioRepository.GetById(servicio.Id);

        if (existente is null)
        {
            throw new InvalidOperationException(
                "El servicio solicitado no existe.");
        }

        servicio.CodigoCatalogo = existente.CodigoCatalogo;
        if (CatalogoServicios.Obtener(existente.CodigoCatalogo) is { } definicion &&
            servicio.UnidadCantidad != definicion.UnidadCantidad)
            throw new InvalidOperationException("La unidad de cobro de un servicio del catálogo no se puede cambiar.");

        servicio.CreadoPor = existente.CreadoPor;
        servicio.FechaCreacion = existente.FechaCreacion;
        servicio.ModificadoPor = UsuarioActual();
        servicio.FechaModificacion = DateTime.Now;

        _servicioRepository.Update(servicio);
    }

    public void Eliminar(int id)
    {
        ValidarExistencia(id);

        Servicio servicio = _servicioRepository.GetById(id)!;
        if (servicio.CodigoCatalogo is not null)
            throw new InvalidOperationException("No se puede eliminar un servicio del catálogo de Nueva Orden.");

        _servicioRepository.Delete(id);
        _auditoria?.Registrar(new RegistroAuditoria
        {
            UsuarioId = _currentUser?.UsuarioId,
            Username = _currentUser?.Username,
            Accion = "SERVICIO_ELIMINADO",
            Entidad = "Servicio",
            EntidadId = id.ToString(),
            Fecha = DateTime.UtcNow,
            Detalle = $"Nombre: {servicio.Nombre}"
        });
    }

    private void ValidarExistencia(int id)
    {
        if (_servicioRepository.GetById(id) is null)
        {
            throw new InvalidOperationException(
                "El servicio solicitado no existe.");
        }
    }

    private string UsuarioActual()
    {
        return string.IsNullOrWhiteSpace(_currentUser?.Username)
            ? "sistema"
            : _currentUser!.Username!;
    }

    private static void ValidarServicio(Servicio servicio)
    {
        if (string.IsNullOrWhiteSpace(servicio.Nombre))
        {
            throw new ArgumentException(
                "El nombre del servicio es obligatorio.");
        }

        if (string.IsNullOrWhiteSpace(servicio.Descripcion))
        {
            throw new ArgumentException(
                "La descripción del servicio es obligatoria.");
        }

        if (servicio.Costo <= 0)
        {
            throw new ArgumentException(
                "El costo debe ser mayor a cero.");
        }

        if (servicio.TiempoEstimadoHoras <= 0)
        {
            throw new ArgumentException(
                "El tiempo estimado debe ser mayor a cero.");
        }

        if (servicio.UnidadCantidad is not null &&
            servicio.UnidadCantidad is not ("llanta" or "retrovisor" or "unidad"))
            throw new ArgumentException("Selecciona una unidad de cobro válida.");
    }
}
