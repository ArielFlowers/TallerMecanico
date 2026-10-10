using System.Security.Cryptography;
using TallerMecanico.Application.Ports;
using TallerMecanico.Models;
using TallerMecanico.Services;
using Xunit;

namespace TallerMecanico.IntegrationTests;

public class VerificacionEmailServiceTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("123")]
    [InlineData("TOKEN_INVALIDO")]
    [InlineData("ZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZ")]
    public void ConfirmarEmail_RechazaTokensInvalidos(string? token)
    {
        var repositorio = new SolicitudesFalsas();
        var servicio = new VerificacionEmailService(repositorio);

        Assert.False(servicio.ConfirmarEmail(token));
        Assert.Null(repositorio.HashRecibido);
    }

    [Fact]
    public void ConfirmarEmail_CalculaHashCorrectamente()
    {
        var repositorio = new SolicitudesFalsas
        {
            ResultadoConfirmacion = true
        };

        var servicio = new VerificacionEmailService(repositorio);

        byte[] bytes = RandomNumberGenerator.GetBytes(32);
        string token = Convert.ToHexString(bytes);

        string hashEsperado = Convert.ToHexString(
            SHA256.HashData(bytes));

        Assert.True(servicio.ConfirmarEmail(token));

        Assert.Equal(hashEsperado, repositorio.HashRecibido);
        Assert.NotEqual(token, repositorio.HashRecibido);
    }

    [Fact]
    public void ConfirmarEmail_DevuelveFalseSiRepositorioRechaza()
    {
        var repositorio = new SolicitudesFalsas
        {
            ResultadoConfirmacion = false
        };

        var servicio = new VerificacionEmailService(repositorio);

        string token = Convert.ToHexString(
            RandomNumberGenerator.GetBytes(32));

        Assert.False(servicio.ConfirmarEmail(token));
        Assert.NotNull(repositorio.HashRecibido);
    }

    private sealed class SolicitudesFalsas : ISolicitudRegistroPort
    {
        public bool ResultadoConfirmacion { get; set; }

        public string? HashRecibido { get; private set; }

                                public SolicitudRegistro? ObtenerPendientePorEmail(string email)
        {
            return null;
        }
        public bool IntentarRenovarTokenConLimite(
            long solicitudId,
            string nuevoTokenHash,
            DateTime nuevaExpiracion)
        {
            return false;
        }
        public bool ConfirmarEmail(string tokenHash)
        {
            HashRecibido = tokenHash;
            return ResultadoConfirmacion;
        }

        public bool ExisteUsername(string username) => false;

        public bool ExisteEmail(string email) => false;

        public void Agregar(SolicitudRegistro solicitud) { }

        public SolicitudRegistro? ObtenerPorId(long id) => null;

        public SolicitudRegistro? ObtenerPorTokenHash(string tokenHash)
            => null;

        public void Actualizar(SolicitudRegistro solicitud) { }
    }
}