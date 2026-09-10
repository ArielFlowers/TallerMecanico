using TallerMecanico.Data;
using TallerMecanico.Models;

namespace TallerMecanico.Services;

public class ServicioService
{
    private readonly IServicioRepository _servicioRepository;

    public ServicioService(IServicioRepository servicioRepository)
    {
        _servicioRepository = servicioRepository;
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
        _servicioRepository.Update(servicio);
    }

    public void Eliminar(int id)
    {
        _servicioRepository.Delete(id);
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
