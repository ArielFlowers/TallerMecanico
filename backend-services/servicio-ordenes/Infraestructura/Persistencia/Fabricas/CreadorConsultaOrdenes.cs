using TallerMecanico.Aplicacion.CasosUso.Ordenes.Contratos;
using TallerMecanico.Infraestructura.Persistencia;
using TallerMecanico.Infraestructura.Persistencia.MySql;

namespace TallerMecanico.Infraestructura.Persistencia.Fabricas;

public sealed class CreadorConsultaOrdenes(DatabaseConnectionFactory connectionFactory)
    : CreadorPuerto<IOrdenQueryRepository>
{
    public override IOrdenQueryRepository CrearRepositorio() =>
        new ConsultaOrdenesRepository(connectionFactory);
}
