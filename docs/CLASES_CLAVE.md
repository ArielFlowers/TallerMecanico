# Clases Clave – TallerMecanico

> Memoria técnica para futuras sesiones. No tocar código de compañeros sin avisar.
> Stack: ASP.NET Core Razor Pages .NET 10 + ADO.NET (`Microsoft.Data.Sqlite` 10.0.11) + SQLite archivo `TallerMecanico.db`.
> No hay backend/frontend separados. Todo es monolito: `Pages/*.cshtml` + `wwwroot/`.

## Cómo correr (verificado 09/09/2026)

```powershell
# SDK 10 requerido. Si solo hay SDK 9, instalar user-local:
# & "$env:TEMP\dotnet-install.ps1" -Channel 10.0 -Quality GA -InstallDir "$env:LOCALAPPDATA\Microsoft\dotnet10"
$env:PATH = "$env:LOCALAPPDATA\Microsoft\dotnet10;$env:PATH"
$env:DOTNET_ROOT = "$env:LOCALAPPDATA\Microsoft\dotnet10"

dotnet restore
dotnet build
dotnet watch run   # http://localhost:5196 | https://localhost:7043
```

- `appsettings.json`: `ConnectionStrings:DefaultConnection = "Data Source=TallerMecanico.db"`.
- La DB se autocrea al arrancar vía `Program.cs -> DatabaseInitializer.Initialize()`.
- Reset DB = borrar `TallerMecanico.db` y reiniciar.
- Puertos en `Properties/launchSettings.json`.

## Arquitectura / Flujo

```
Program.cs (DI Scoped)
 -> Pages/*.cshtml.cs (PageModel OnGet/OnPost)
 -> Services/* (validación + normalización)
 -> Data/*Repository (SQL parametrizado)
 -> SQLite (TallerMecanico.db)
 -> Razor (.cshtml) + ViewModel
```

Rutas: `/` Dashboard, `/Mecanicos` (CRUD en 1 página con handlers), `/Servicios`, `/Servicios/Create`, `/Servicios/Edit?id=`, `/Servicios/Delete?id=`, `/Historial`, `/Privacy`, `/Error`.

## Models/ (POCOs sin lógica)

| Archivo | Props |
|---|---|
| `Models/Mecanico.cs` | `Id:int, Ci:string, NombreCompleto:string, Especialidad:string, Celular:string` |
| `Models/Servicio.cs` | `Id:int, Nombre:string, Descripcion:string, Costo:decimal, TiempoEstimadoHoras:decimal` |
| `Models/HistorialCostoServicio.cs` | `Id:int, ServicioId:int, NombreServicio:string, CostoAnterior:decimal, CostoNuevo:decimal, FechaCambio:DateTime` |

## Data/ (acceso SQLite crudo)

| Archivo | Responsabilidad / Métodos |
|---|---|
| `Data/DatabaseConnection.cs` | Factory. `CreateConnection(): SqliteConnection`. Lee `DefaultConnection`, lanza si falta. Dep: `IConfiguration`. |
| `Data/DatabaseInitializer.cs` | Migración al arranque. `Initialize()`, `CreateMecanicosTable()`, `CreateServiciosTable()`, `CreateHistorialCostoServiciosTable()`, `CreateHistorialCostoServicioTrigger()`, `ExecuteCommand()`. Todo `IF NOT EXISTS`. |
| `Data/MecanicoRepository.cs` + `IMecanicoRepository.cs` | CRUD async. `CrearAsync()->int (RETURNING Id)`, `ObtenerAsync(termino?)`, `ActualizarAsync()->bool`, `EliminarAsync()->bool`, `ExisteCiAsync(ci,idExcluido?)->bool`. Búsqueda `LIKE ESCAPE '\' COLLATE NOCASE` en 4 campos. |
| `Data/ServicioRepository.cs` (sin interfaz) | CRUD sync. `GetAll(), GetById(id), Add(), Update(), Delete(), Count()`, helpers `AddParameters(), MapServicio()`. |
| `Data/HistorialCostoServicioRepository.cs` + `IHistorialCostoServicioRepository.cs` | Solo lectura. `GetAll()->IReadOnlyList` orden `FechaCambio DESC, Id DESC`. |

Tablas:
- `Mecanicos(Id PK AI, Ci TEXT UNIQUE NOT NULL, NombreCompleto, Especialidad, Celular)`.
- `Servicios(Id PK AI, Nombre, Descripcion TEXT NOT NULL, Costo REAL CHECK>0, TiempoEstimadoHoras REAL CHECK>0)`.
- `HistorialCostoServicios(Id PK AI, ServicioId INT, NombreServicio TEXT, CostoAnterior REAL, CostoNuevo REAL, FechaCambio TEXT)`.
- Trigger `TRG_Servicios_HistorialCosto`: `AFTER UPDATE OF Costo ON Servicios WHEN OLD.Costo<>NEW.Costo` → `INSERT Historial(... datetime('now','localtime'))`.

## Services/ (regla de negocio, `Scoped` en `Program.cs`)

| Archivo | Métodos / Notas |
|---|---|
| `Services/MecanicoService.cs` | `CrearAsync(Input)->(Id?,Errores)`, `ObtenerAsync(termino?)`, `ActualizarAsync(id,Input)->(bool,Errores)`, `EliminarAsync(id)`. Normaliza: trim, colapsa `\s+`, CI complemento a mayúsculas. Solo chequea CI duplicado si pasa `ValidacionMecanicos`. Dep: `IMecanicoRepository, ValidacionMecanicos`. |
| `Services/ServicioService.cs` | `ObtenerTodos(), ObtenerPorId(), Crear(), Actualizar(), Eliminar()`. `ValidarServicio()` lanza `ArgumentException` si nombre/descripción vacíos o `Costo/Tiempo<=0`. Dep: `ServicioRepository`. |
| `Services/DashboardService.cs` | `GetDashboardData()->DashboardViewModel`. Hoy: `Mecanicos=0, Vehiculos=0` hardcodeados, `Servicios=Count()` real. Dep: `ServicioRepository`. |
| `Services/HistorialCostoServicioService.cs` + `IHistorialCostoServicioService.cs` | Fachada fina. `ObtenerHistorial()` → repo. |

## Validators/

`Validators/ValidacionMecanicos.cs` – puro, retorna `Dictionary<campo,msg>`.
- `Validar(Input)` → `ValidarCi(), ValidarNombreCompleto(), ValidarEspecialidad(), ValidarCelular()`.
- CI: `5-8 dígitos + opcional -complemento 1-2 alfanumérico`.
- Nombre `<=100`, Especialidad `<=60`.
- Celular `==8 dígitos, solo [0-9], inicia 6|7`.

## ViewModels/ (DTOs binding)

- `ViewModels/MecanicoInputModel.cs`: `Ci,NombreCompleto,Especialidad,Celular` plano (sin DataAnnotations).
- `ViewModels/ServicioFormViewModel.cs`: `[Required]` Nombre/Descripcion, `[Range(0.01,double.MaxValue)]` Costo/Tiempo.
- `ViewModels/DashboardViewModel.cs`: `MecanicosDisponibles, VehiculosRegistrados, ServiciosRegistrados:int`.

## Pages/**/*.cshtml.cs

- `Pages/Index.cshtml.cs`: `OnGet() -> DashboardService.GetDashboardData()`.
- `Pages/Mecanicos/Index.cshtml.cs`: CRUD por modales/handlers. `OnGetAsync()->ObtenerAsync(TerminoBusqueda)`, `OnPostCrearAsync()->CrearAsync`, `OnPostActualizarAsync()->ActualizarAsync`, `OnPostEliminarAsync()->EliminarAsync`. Usa `TempData MensajeExito/Error`, `FormularioActivo=crear/editar`.
- `Pages/Servicios/Index.cshtml.cs`: `OnGet()->ObtenerTodos()`.
- `Pages/Servicios/Create/Edit/Delete.cshtml.cs`: `OnGet(id)`, `OnPost()->Crear/Actualizar/Eliminar()` con `ModelState.IsValid` + trim.
- `Pages/Historial/Index.cshtml.cs`: `OnGet()->ObtenerHistorial()`.
- `Privacy, Error`: plantilla default.

## Frontend (`wwwroot/` + `Pages/Shared/_Layout.cshtml`)

- `css/site.css, mecanicos.css`, `js/site.js, mecanicos.js`, `lib/bootstrap,jquery,jquery-validation(-unobtrusive)`, `favicon.ico`, `images/hero-taller.jpg`.
- `Program.cs`: `MapStaticAssets()+MapRazorPages().WithStaticAssets(), UseHttpsRedirection/Routing/Authorization`.
- Sidebar con placeholders `href="#"` (Personal, Clientes/Vehículos, Operaciones, Inventario, Administración). Solo activos: Inicio/Mecánicos/Servicios/Historial/Vehículos.

## Home / Inicio (`feature/home-inicio`, 10/09/2026)

> Rama: `feature/home-inicio` (desde `main`). Sin cambios de backend: no se tocó `Index.cshtml.cs`, `DashboardService`, repos ni trigger.

- Imagen verificada: no existe `watermarked_img_11084677959285558999.jpg` en el repo. Se usó `docs/Gemini_Generated_Image_qq3q1yqq3q1yqq3q.jfif` (2.5 MB) copiada a `wwwroot/images/hero-taller.jpg`.
- `Pages/Index.cshtml` (`Title="Inicio"`): `section.home-hero` (fondo + overlay `rgba(13,10,18,.72)`, título "Potencia la eficiencia..." + subtítulo, sin botones por decisión), `section.home-features` grid 3 cards (`/Vehiculos`, `/Mecanicos`, `/Servicios`), bloque `.dashboard` + `.metrics-grid` conservado intacto, `section.home-audit` (link `/Historial`), `footer.home-footer` ("AutoTaller Pro - v1.0").
- `wwwroot/css/site.css`: bloque `INICIO / HOME` (`.home`, `.home-hero`, `.home-features`, `.home-card`, `.home-audit`, `.home-footer`), reutiliza `--primary:#7c3aed` y superficies grises, responsive 3→1 col. Fondo: `url("../images/hero-taller.jpg")`.
- Verificado: `dotnet build` OK (0 warnings/errors).

## Convenciones para continuar

1. Mecánicos = patrón ideal: `InputModel -> ValidacionMecanicos -> MecanicoService (normaliza) -> IMecanicoRepository (async)`.
2. Servicios = patrón simple a migrar si se toca: validación imperativa + repo sync sin interfaz.
3. No usar EF Core. Todo SQL parametrizado directo.
4. Histórico es automático por trigger, no meter lógica C# para eso.
5. Futura Tabla 3 debe replicar patrón Mecánicos (interfaz async + validador + InputModel con 4+ attrs).
