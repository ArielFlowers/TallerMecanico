using System.Data.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging.Abstractions;
using TallerMecanico.Application.Ordenes;
using TallerMecanico.Application.Ports;
using TallerMecanico.Models;
using TallerMecanico.Pages.Ordenes;
using TallerMecanico.Services;

namespace TallerMecanico.IntegrationTests;

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
        var fachada = SoporteComprobantePdf.CrearFachada(new ConsultaOrdenesService(consultas, usuario), usuario, generador, reloj);

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
        var fachada = SoporteComprobantePdf.CrearFachada(new ConsultaOrdenesService(consultas, usuario), usuario, generador);
        Assert.Throws<UnauthorizedAccessException>(() => fachada.GenerarComprobante(15));
        Assert.Equal(0, consultas.Consultas);
        Assert.Null(generador.Orden);
    }

    [Theory]
    [InlineData(0, 400)]
    [InlineData(-1, 400)]
    [InlineData(999, 404)]
    public void Endpoint_IdInvalidoOInexistenteNoGeneraPdf(int id, int codigo)
    {
        var pagina = Pagina(new LecturaPrueba(), new GeneradorPrueba());
        Assert.Equal(codigo, Assert.IsAssignableFrom<ObjectResult>(pagina.OnGet(id)).StatusCode);
    }

    [Fact]
    public void Endpoint_EntregaBytesPdfConNombreInline()
    {
        var generador = new GeneradorPrueba();
        var pagina = Pagina(new LecturaPrueba(), generador);
        var respuesta = Assert.IsType<FileContentResult>(pagina.OnGet(15));
        Assert.Equal("application/pdf", respuesta.ContentType);
        Assert.Same(generador.Pdf, respuesta.FileContents);
        Assert.Contains("inline", pagina.Response.Headers.ContentDisposition.ToString());
        Assert.Contains("Comprobante-Orden-15.pdf", pagina.Response.Headers.ContentDisposition.ToString());
    }

    [Fact]
    public void Endpoint_ErrorConsultaDevuelve503SinDetallesTecnicos()
    {
        var pagina = Pagina(new LecturaPrueba { Fallar = true }, new GeneradorPrueba());
        var resultado = Assert.IsType<ObjectResult>(pagina.OnGet(15));
        Assert.Equal(503, resultado.StatusCode);
        Assert.Equal("No se pudo consultar la orden. Intenta nuevamente.", resultado.Value);
    }

    [Fact]
    public void Endpoint_ErrorGeneracionDevuelve500SinDetallesTecnicos()
    {
        var pagina = Pagina(new LecturaPrueba(), new GeneradorPrueba { Fallar = true });
        var resultado = Assert.IsType<ObjectResult>(pagina.OnGet(15));
        Assert.Equal(500, resultado.StatusCode);
        Assert.Equal("No se pudo generar el comprobante. Intenta nuevamente.", resultado.Value);
    }

    private static ComprobanteModel Pagina(LecturaPrueba puerto, GeneradorPrueba generador)
    {
        var usuario = new UsuarioPrueba();
        return new ComprobanteModel(SoporteComprobantePdf.CrearFachada(new ConsultaOrdenesService(puerto, usuario), usuario, generador),
            NullLogger<ComprobanteModel>.Instance)
        { PageContext = new PageContext { HttpContext = new DefaultHttpContext() } };
    }

    private sealed class LecturaPrueba : IConsultaOrdenesPort
    {
        public OrdenDetalle Orden { get; } = SoporteComprobantePdf.Orden();
        public int Consultas { get; private set; }
        public bool Fallar { get; init; }
        public IReadOnlyList<OrdenResumen> Obtener(EstadoOrden? estado = null, DateOnly? fecha = null) => [];
        public OrdenDetalle? ObtenerDetalle(int id)
        {
            Consultas++;
            if (Fallar) throw new ErrorConsulta();
            return id == Orden.Id ? Orden : null;
        }
    }

    private sealed class GeneradorPrueba : IComprobanteOrdenPdf
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
