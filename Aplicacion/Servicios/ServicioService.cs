using System.Text.RegularExpressions;
using TallerMecanico.Aplicacion.Dto;
using TallerMecanico.Aplicacion.Puertos;
using TallerMecanico.Dominio.Entidades;
using TallerMecanico.Infraestructura.Persistencia.Fabricas;

namespace TallerMecanico.Aplicacion.Servicios;

public class ServicioService : IServicioService
{
    private const int MinimoCaracteresSugerencias = 2;
    private const int MaximoCaracteresSugerencias = 100;

    private readonly IRepository<Servicio> _servicioRepository;
    private readonly IConsultaServiciosPort _consultaServicios;
    private readonly ICurrentUser? _currentUser;
    private readonly ICacheDistribuidaPort? _cache;

    public ServicioService(
        CreadorServicio creadorServicio,
        ICurrentUser? currentUser = null,
        ICacheDistribuidaPort? cache = null)
    {
        _servicioRepository = creadorServicio.CrearRepositorio();
        _consultaServicios = (IConsultaServiciosPort)_servicioRepository;
        _currentUser = currentUser;
        _cache = cache;
    }

    public List<Servicio> ObtenerTodos()
    {
        return _servicioRepository.GetAll();
    }

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
        string? termino, CancellationToken cancellationToken = default)
    {
        var terminoNormalizado = Regex.Replace(termino?.Trim() ?? string.Empty, @"\s+", " ");
        if (terminoNormalizado.Length < MinimoCaracteresSugerencias ||
            terminoNormalizado.Length > MaximoCaracteresSugerencias)
        {
            return Task.FromResult<IReadOnlyList<SugerenciaServicio>>([]);
        }

        return _consultaServicios.SugerirAsync(terminoNormalizado, cancellationToken);
    }

    public void Crear(Servicio servicio)
    {
        ValidarServicio(servicio);

        servicio.CreadoPor = UsuarioActual();
        servicio.FechaCreacion = DateTime.Now;

        _servicioRepository.Add(servicio);
        _cache?.Eliminar(ClavesCache.DashboardResumen);
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

        servicio.CreadoPor = existente.CreadoPor;
        servicio.FechaCreacion = existente.FechaCreacion;
        servicio.ModificadoPor = UsuarioActual();
        servicio.FechaModificacion = DateTime.Now;

        _servicioRepository.Update(servicio);
    }

    public void Eliminar(int id)
    {
        ValidarExistencia(id);

        _servicioRepository.Delete(id);
        _cache?.Eliminar(ClavesCache.DashboardResumen);
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
    }
}
