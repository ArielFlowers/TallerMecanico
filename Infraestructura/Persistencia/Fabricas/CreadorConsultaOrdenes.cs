using TallerMecanico.Aplicacion.Puertos;
using TallerMecanico.Infraestructura.Persistencia;
using TallerMecanico.Infraestructura.Persistencia.MySql;

namespace TallerMecanico.Infraestructura.Persistencia.Fabricas;

public sealed class CreadorConsultaOrdenes(DatabaseConnectionFactory connectionFactory)
    : CreadorPuerto<IConsultaOrdenesPort>
{
    public override IConsultaOrdenesPort CrearRepositorio() =>
        new ConsultaOrdenesRepository(connectionFactory);
}
