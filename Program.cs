using TallerMecanico.Data;
using TallerMecanico.Services;
using TallerMecanico.Validators;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();

builder.Services.AddScoped<DatabaseConnection>();
builder.Services.AddScoped<DatabaseInitializer>();

builder.Services.AddScoped<IMecanicoRepository, MecanicoRepository>();
builder.Services.AddScoped<ValidacionMecanicos>();
builder.Services.AddScoped<MecanicoService>();

builder.Services.AddScoped<ServicioRepository>();
builder.Services.AddScoped<ServicioService>();

builder.Services.AddScoped<
    IHistorialCostoServicioRepository,
    HistorialCostoServicioRepository>();

builder.Services.AddScoped<
    IHistorialCostoServicioService,
    HistorialCostoServicioService>();

builder.Services.AddScoped<DashboardService>();

var app = builder.Build();

using (IServiceScope scope = app.Services.CreateScope())
{
    DatabaseInitializer databaseInitializer =
        scope.ServiceProvider.GetRequiredService<DatabaseInitializer>();

    databaseInitializer.Initialize();
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseRouting();

app.UseAuthorization();

app.MapStaticAssets();
app.MapRazorPages()
   .WithStaticAssets();

app.Run();