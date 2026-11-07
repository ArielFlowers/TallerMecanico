using System.Data.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging.Abstractions;
using TallerMecanico.Aplicacion.CasosUso.Ordenes;
using TallerMecanico.Aplicacion.CasosUso.Ordenes.Contratos;
using TallerMecanico.Dominio.Entidades;
using TallerMecanico.Aplicacion.Servicios;

namespace TallerMecanico.IntegrationTests;

[Trait("Area", "Backend")]
public sealed class ComprobanteOrdenTests
{
    [Theory]
    [InlineData("Administrador")]
    [InlineData("Recepcionista")]
    public void Fachada_GeneraParaRolesAutorizadosConIdentidadYFechaDelServidor(string rol)
    {
        var usuario = new UsuarioPrueba { Username = "generador_actual", Rol = rol };
        var consultas = new LecturaPrueba();
        var generador = new GeneradorPrueba();
        var reloj = new RelojPrueba();
        var fachada = SoporteComprobantePdf.CrearFachada(new ConsultarOrdenesCasoUso(consultas, usuario), usuario, generador, reloj);

        Assert.Same(generador.Pdf, fachada.GenerarComprobante(15));
        Assert.Same(consultas.Orden, generador.Orden);
        Assert.Equal("Son Treinta y Un Bolivianos con 50/100.", generador.Literal);
        Assert.Equal(usuario.Username, generador.Usuario);
        Assert.Equal(reloj.Ahora, generador.Fecha);
        usuario.Username = "otro_generador";
        reloj.Ahora += TimeSpan.FromMinutes(1);
        fachada.GenerarComprobante(15);
        Assert.Equal("otro_generador", generador.Usuario);
        Assert.Equal(reloj.Ahora, generador.Fecha);
        Assert.Equal("creador_original", consultas.Orden.CreadoPor);
        Assert.Equal(2, consultas.Consultas);
    }

    [Theory]
    [InlineData(false, "Administrador")]
    [InlineData(true, "Invitado")]
    public void Seguridad_NoConsultaNiGeneraSinPermiso(bool autenticado, string rol)
    {
        var usuario = new UsuarioPrueba { EstaAutenticado = autenticado, Rol = rol };
        var consultas = new LecturaPrueba();
        var generador = new GeneradorPrueba();
        var fachada = SoporteComprobantePdf.CrearFachada(new ConsultarOrdenesCasoUso(consultas, usuario), usuario, generador);
        Assert.Throws<UnauthorizedAccessException>(() => fachada.GenerarComprobante(15));
        Assert.Equal(0, consultas.Consultas);
        Assert.Null(generador.Orden);
    }

    private sealed class LecturaPrueba : IOrdenQueryRepository
    {
        public OrdenDetalle Orden { get; } = SoporteComprobantePdf.Orden();
        public int Consultas { get; private set; }
        public bool Fallar { get; init; }
        public IReadOnlyList<OrdenResumen> Obtener(EstadoOrden? estado = null, DateOnly? fecha = null,
            string? terminoBusqueda = null, int? ordenSeleccionadaId = null) => [];
        public Task<IReadOnlyList<SugerenciaOrden>> SugerirAsync(string termino, EstadoOrden? estado = null,
            DateOnly? fecha = null, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<SugerenciaOrden>>([]);
        public OrdenDetalle? ObtenerDetalle(int id)
        {
            Consultas++;
            if (Fallar) throw new ErrorConsulta();
            return id == Orden.Id ? Orden : null;
        }
    }

    private sealed class GeneradorPrueba : IComprobantePdf
    {
        public byte[] Pdf { get; } = "%PDF-prueba"u8.ToArray();
        public OrdenDetalle? Orden { get; private set; }
        public string? Literal { get; private set; }
        public string? Usuario { get; private set; }
        public DateTimeOffset Fecha { get; private set; }
        public bool Fallar { get; init; }
        public byte[] Generar(OrdenDetalle orden, string totalLiteral, string generadoPor, DateTimeOffset fechaGeneracion)
        {
            if (Fallar) throw new InvalidOperationException("Error interno de prueba");
            Orden = orden; Literal = totalLiteral; Usuario = generadoPor; Fecha = fechaGeneracion;
            return Pdf;
        }
    }

    private sealed class RelojPrueba : TimeProvider
    {
        public DateTimeOffset Ahora { get; set; } = new(2026, 10, 12, 20, 30, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Ahora;
    }

    private sealed class ErrorConsulta() : DbException("Error interno de prueba");
}
