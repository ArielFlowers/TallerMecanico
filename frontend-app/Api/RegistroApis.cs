namespace TallerMecanico.Api;

// Registra un cliente HTTP por cada servicio backend.
public static class RegistroApis
{
    public static IServiceCollection AddApisInternas(this IServiceCollection servicios, IConfiguration configuracion)
    {
        servicios.AddTransient<CabecerasInternasHandler>();
        Registrar<OrdenesApi>(servicios, configuracion, "Servicios:OrdenesUrl");
        Registrar<ClientesApi>(servicios, configuracion, "Servicios:OrdenesUrl");
        Registrar<VehiculosApi>(servicios, configuracion, "Servicios:OrdenesUrl");
        Registrar<ProductosApi>(servicios, configuracion, "Servicios:OrdenesUrl");
        Registrar<MecanicosApi>(servicios, configuracion, "Servicios:OrdenesUrl");
        Registrar<ServiciosApi>(servicios, configuracion, "Servicios:OrdenesUrl");
        Registrar<UsuariosApi>(servicios, configuracion, "Servicios:UsuariosUrl");
        Registrar<ReportesApi>(servicios, configuracion, "Servicios:ReportesUrl");
        return servicios;
    }

    private static void Registrar<TApi>(IServiceCollection servicios, IConfiguration configuracion, string claveUrl)
        where TApi : class =>
        servicios.AddHttpClient<TApi>(cliente => cliente.BaseAddress = new Uri(configuracion[claveUrl]
                ?? throw new InvalidOperationException($"Configura {claveUrl} en el Frontend.")))
            .AddHttpMessageHandler<CabecerasInternasHandler>();
}
