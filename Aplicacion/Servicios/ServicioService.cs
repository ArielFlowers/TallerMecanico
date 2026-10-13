using TallerMecanico.Aplicacion.Puertos;
using TallerMecanico.Infraestructura.Persistencia;
using TallerMecanico.Dominio.Entidades;
using TallerMecanico.Infraestructura.Persistencia.Fabricas;

namespace TallerMecanico.Aplicacion.Servicios;

public class ServicioService : IServicioService
{
    private readonly IRepository<Servicio> _servicioRepository;
    private readonly ICurrentUser? _currentUser;

    public ServicioService(
        CreadorServicio creadorServicio,
        ICurrentUser? currentUser = null)
    {
        _servicioRepository = creadorServicio.CrearRepositorio();
        _currentUser = currentUser;
    }

    public List<Servicio> ObtenerTodos()
    {
        return _servicioRepository.GetAll();
    }

    public Servicio? ObtenerPorId(int id)
    {
        return _servicioRepository.GetById(id);
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
