using TallerMecanico.Application.Ports;
using TallerMecanico.Data.Factories;
using TallerMecanico.Infraestructura.Adapters.MySql;

namespace TallerMecanico.Patterns.FactoryMethod;

public sealed class CreadorConsultaOrdenes(DatabaseConnectionFactory connectionFactory)
    : CreadorPuerto<IConsultaOrdenesPort>
{
    public override IConsultaOrdenesPort CrearRepositorio() =>
        new ConsultaOrdenesRepository(connectionFactory);
}
