using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.RazorPages;
using StackExchange.Redis;
using TallerMecanico.Infraestructura.Sesion;

namespace TallerMecanico.IntegrationTests;

public sealed class SesionDisponibleTests
{
    [Fact]
    public async Task SinUsoDeBorradores_NoTocaAlmacenamientoNiBloqueaDashboard()
    {
        var sesion = new SesionControlada { FalloCarga = new TimeoutException(), FalloCommit = new TimeoutException() };
        var (entrada, salida) = Contextos(sesion);
        await Filtro().OnPageHandlerExecutionAsync(entrada, () => Task.FromResult(salida));
        Assert.Equal(0, sesion.Cargas);
        Assert.Equal(0, sesion.Commits);
        Assert.IsType<PageResult>(salida.Result);
    }

    [Fact]
    public async Task BorradorUsado_ConfirmaAntesDeEnviarRedireccion()
    {
        var sesion = new SesionControlada();
        var (entrada, salida) = Contextos(sesion);
        salida.Result = new RedirectToPageResult("/Vehiculos/Index");
        await Filtro().OnPageHandlerExecutionAsync(entrada, () =>
        {
            SesionDisponible.Obtener(entrada.HttpContext).SetString("prueba", "contenido");
            return Task.FromResult(salida);
        });
        Assert.Equal(1, sesion.Cargas);
        Assert.Equal(1, sesion.Commits);
        Assert.IsType<RedirectToPageResult>(salida.Result);
        Assert.Null(salida.Exception);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task FalloCarga_NoSeConfundeConSesionVencidaYNuncaConfirma(bool timeout)
    {
        var sesion = new SesionControlada { FalloCarga = ErrorRedis(timeout) };
        var (entrada, salida) = Contextos(sesion);
        var logger = new RegistradorCachePrueba<ConfirmarSesionFilter>();
        await new ConfirmarSesionFilter(logger).OnPageHandlerExecutionAsync(entrada, () =>
        {
            salida.Exception = Record.Exception(() => SesionDisponible.Obtener(entrada.HttpContext));
            return Task.FromResult(salida);
        });
        Comprobar503(salida, logger, "Carga");
        Assert.Equal(0, sesion.Commits);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task FalloCommit_NoDevuelveRedireccionExitosaNiExponeExcepcion(bool timeout)
    {
        var sesion = new SesionControlada { FalloCommit = ErrorRedis(timeout) };
        var (entrada, salida) = Contextos(sesion);
        var logger = new RegistradorCachePrueba<ConfirmarSesionFilter>();
        await new ConfirmarSesionFilter(logger).OnPageHandlerExecutionAsync(entrada, () =>
        {
            SesionDisponible.Obtener(entrada.HttpContext).SetString("prueba", "contenido");
            salida.Result = new RedirectToPageResult("/Clientes/Index");
            return Task.FromResult(salida);
        });
        Comprobar503(salida, logger, "Confirmacion");
        Assert.Equal(1, sesion.Commits);
        Assert.Equal("contenido", sesion.GetString("prueba")); // No elimina ni revierte otros datos.
    }

    [Fact]
    public async Task ErrorAjenoASesion_NoSeEnmascaraComoFalloRedis()
    {
        var sesion = new SesionControlada();
        var (entrada, salida) = Contextos(sesion);
        var error = new InvalidOperationException("Error ajeno");
        await Filtro().OnPageHandlerExecutionAsync(entrada, () =>
        {
            SesionDisponible.Obtener(entrada.HttpContext);
            salida.Exception = error;
            return Task.FromResult(salida);
        });
        Assert.Same(error, salida.Exception);
        Assert.False(salida.ExceptionHandled);
        Assert.Equal(0, sesion.Commits);
    }

    [Fact]
    public void PeticionCancelada_NoSeConvierteEn503()
    {
        var sesion = new SesionControlada { FalloCarga = new OperationCanceledException() };
        var contexto = new DefaultHttpContext { Session = sesion, RequestAborted = new CancellationToken(true) };
        Assert.Throws<OperationCanceledException>(() => SesionDisponible.Obtener(contexto));
    }

    private static Exception ErrorRedis(bool timeout) => timeout ? new TimeoutException("Password=secreto-no-registrar")
        : new RedisConnectionException(ConnectionFailureType.UnableToConnect, "Password=secreto-no-registrar");

    private static ConfirmarSesionFilter Filtro() => new(new RegistradorCachePrueba<ConfirmarSesionFilter>());

    private static void Comprobar503(PageHandlerExecutedContext contexto, RegistradorCachePrueba<ConfirmarSesionFilter> logger, string operacion)
    {
        var resultado = Assert.IsType<ContentResult>(contexto.Result);
        Assert.Equal(503, resultado.StatusCode);
        Assert.Contains("No se pudo confirmar el estado de la sesión", resultado.Content);
        Assert.DoesNotContain("secreto-no-registrar", resultado.Content);
        Assert.True(contexto.ExceptionHandled);
        Assert.Equal("no-store", contexto.HttpContext.Response.Headers.CacheControl);
        Assert.Contains(logger.Mensajes, mensaje => mensaje.Contains(operacion));
        Assert.DoesNotContain(logger.Mensajes, mensaje => mensaje.Contains("secreto-no-registrar"));
    }

    private static (PageHandlerExecutingContext Entrada, PageHandlerExecutedContext Salida) Contextos(ISession sesion)
    {
        var pagina = new PageContext
        {
            HttpContext = new DefaultHttpContext { Session = sesion },
            RouteData = new Microsoft.AspNetCore.Routing.RouteData(),
            ActionDescriptor = new CompiledPageActionDescriptor()
        };
        var instancia = new object();
        return (new PageHandlerExecutingContext(pagina, [], null, new Dictionary<string, object?>(), instancia),
            new PageHandlerExecutedContext(pagina, [], null, instancia) { Result = new PageResult() });
    }

    private sealed class SesionControlada : ISession
    {
        private readonly Dictionary<string, byte[]> _datos = [];
        public Exception? FalloCarga { get; init; }
        public Exception? FalloCommit { get; init; }
        public int Cargas { get; private set; }
        public int Commits { get; private set; }
        public bool IsAvailable => true;
        public string Id => "sesion-aislada-prueba";
        public IEnumerable<string> Keys => _datos.Keys;
        public void Clear() => _datos.Clear();
        public void Remove(string key) => _datos.Remove(key);
        public void Set(string key, byte[] value) => _datos[key] = value;
        public bool TryGetValue(string key, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out byte[]? value) => _datos.TryGetValue(key, out value);
        public Task LoadAsync(CancellationToken cancellationToken = default)
        { Cargas++; return FalloCarga is null ? Task.CompletedTask : Task.FromException(FalloCarga); }
        public Task CommitAsync(CancellationToken cancellationToken = default)
        { Commits++; return FalloCommit is null ? Task.CompletedTask : Task.FromException(FalloCommit); }
    }
}
