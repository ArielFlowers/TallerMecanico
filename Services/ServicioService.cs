using TallerMecanico.Data;
using TallerMecanico.Models;
using TallerMecanico.Patterns.FactoryMethod;

namespace TallerMecanico.Services;

public class ServicioService : IServicioService
{
    private readonly IRepository<Servicio> _servicioRepository;

    public ServicioService(CreadorServicio creadorServicio)
    {
        _servicioRepository = creadorServicio.CrearRepositorio();
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

        _servicioRepository.Add(servicio);
    }

    public void Actualizar(Servicio servicio)
    {
        ValidarServicio(servicio);
        ValidarExistencia(servicio.Id);

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
