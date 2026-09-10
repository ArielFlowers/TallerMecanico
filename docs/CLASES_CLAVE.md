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

Rutas: `/` Inicio/Dashboard, `/Mecanicos` (CRUD en 1 página con handlers), `/Vehiculos` (CRUD en 1 página con modales), `/Servicios`, `/Servicios/Create`, `/Servicios/Edit?id=`, `/Servicios/Delete?id=`, `/Servicios/Control`, `/Servicios/Registros`, `/Historial`, `/Privacy`, `/Error`. Ramas: `dev1/setup-dashboard` (US01), `dev2/crud-mecanicos` (US02), `feature/us03-crud-vehiculos` (US03), `dev4/crud-servicios` (US04), `feature/us05-historial-costo-servicios` (US05), `feature/home-inicio` (Home).

## Models/ (POCOs sin lógica)

| Archivo | Props |
|---|---|
| `Models/Mecanico.cs` | `Id:int, Ci:string, NombreCompleto:string, Especialidad:string, Celular:string` |
| `Models/Servicio.cs` | `Id:int, Nombre:string, Descripcion:string, Costo:decimal, TiempoEstimadoHoras:decimal` |
| `Models/Vehiculo.cs` | `Id:int, Placa:string (UNIQUE), Modelo:string, Kilometraje:int, Observaciones:string` |
| `Models/HistorialCostoServicio.cs` | `Id:int, ServicioId:int, NombreServicio:string, CostoAnterior:decimal, CostoNuevo:decimal, FechaCambio:DateTime` |

## Data/ (acceso SQLite crudo)

| Archivo | Responsabilidad / Métodos |
|---|---|
| `Data/DatabaseConnection.cs` | Factory. `CreateConnection(): SqliteConnection`. Lee `DefaultConnection`, lanza si falta. Dep: `IConfiguration`. |
| `Data/DatabaseInitializer.cs` | Migración al arranque. `Initialize()`, `CreateMecanicosTable()`, `CreateServiciosTable()`, `CreateVehiculosTable()`, `CreateHistorialCostoServiciosTable()`, `CreateHistorialCostoServicioTrigger()`, `ExecuteCommand()`. Todo `IF NOT EXISTS`. |
| `Data/MecanicoRepository.cs` + `IMecanicoRepository.cs` | CRUD async. `CrearAsync()->int (RETURNING Id)`, `ObtenerAsync(termino?)`, `ActualizarAsync()->bool`, `EliminarAsync()->bool`, `ExisteCiAsync(ci,idExcluido?)->bool`. Búsqueda `LIKE ESCAPE '\' COLLATE NOCASE` en 4 campos. |
| `Data/ServicioRepository.cs` + `IServicioRepository.cs` | CRUD. `GetAll(), GetById(id), Add(), Update(), Delete(), Count()`, helpers `AddParameters(), MapServicio()`. Listado `ORDER BY Nombre`. (US04-Aldair agregó la interfaz; antes era repo sync sin interfaz.) |
| `Data/VehiculoRepository.cs` + `IVehiculoRepository.cs` | CRUD con ADO.NET parametrizado. Placa `UNIQUE`, `Count()` para el Dashboard. (US03-Adrian.) |
| `Data/HistorialCostoServicioRepository.cs` + `IHistorialCostoServicioRepository.cs` | Solo lectura. `GetAll()->IReadOnlyList` orden `FechaCambio DESC, Id DESC`. |

Tablas:
- `Mecanicos(Id PK AI, Ci TEXT UNIQUE NOT NULL, NombreCompleto, Especialidad, Celular)`.
- `Servicios(Id PK AI, Nombre, Descripcion TEXT NOT NULL, Costo REAL CHECK>0, TiempoEstimadoHoras REAL CHECK>0)`.
- `Vehiculos(Id PK AI, Placa TEXT UNIQUE NOT NULL, Modelo, Kilometraje, Observaciones)`.
- `HistorialCostoServicios(Id PK AI, ServicioId INT, NombreServicio TEXT, CostoAnterior REAL, CostoNuevo REAL, FechaCambio TEXT)`.
- Trigger `TRG_Servicios_HistorialCosto`: `AFTER UPDATE OF Costo ON Servicios WHEN OLD.Costo<>NEW.Costo` → `INSERT Historial(... datetime('now','localtime'))`.

## Services/ (regla de negocio, `Scoped` en `Program.cs`)

| Archivo | Métodos / Notas |
|---|---|
| `Services/MecanicoService.cs` | `CrearAsync(Input)->(Id?,Errores)`, `ObtenerAsync(termino?)`, `ActualizarAsync(id,Input)->(bool,Errores)`, `EliminarAsync(id)`. Normaliza: trim, colapsa `\s+`, CI complemento a mayúsculas. Solo chequea CI duplicado si pasa `ValidacionMecanicos`. Dep: `IMecanicoRepository, ValidacionMecanicos`. |
| `Services/ServicioService.cs` | `ObtenerTodos(), ObtenerPorId(), Crear(), Actualizar(), Eliminar()`. Valida vía `ServicioFormViewModel` + `ValidacionServicios` + checks de SQLite (nombre/descripción obligatorios, `Costo/Tiempo>0`, sin espacios inválidos ni caracteres no permitidos). Dep: `IServicioRepository`. |
| `Services/VehiculoService.cs` + `IVehiculoService.cs` | Coordina normalización de placa (mayúsculas) + validación + `IVehiculoRepository`. (US03-Adrian.) Dep: `IVehiculoRepository`. |
| `Services/DashboardService.cs` | `GetDashboardData()->DashboardViewModel`. `Vehiculos=Count()` real y `Servicios=Count()` real; `Mecanicos=0` sigue hardcodeado (pendiente conectar). Dep: `IServicioRepository, IVehiculoRepository`. |
| `Services/HistorialCostoServicioService.cs` + `IHistorialCostoServicioService.cs` | Fachada fina. `ObtenerHistorial()` → repo. |

## Validators/

- `Validators/ValidacionMecanicos.cs` – puro, retorna `Dictionary<campo,msg>`.
  - `Validar(Input)` → `ValidarCi(), ValidarNombreCompleto(), ValidarEspecialidad(), ValidarCelular()`.
  - CI: `5-8 dígitos + opcional -complemento 1-2 alfanumérico`.
  - Nombre `<=100`, Especialidad `<=60`, sin números, obligatorios.
  - Celular `==8 dígitos, solo [0-9], inicia 6|7`.
- `Validators/ValidacionServicios.cs` – validación del catálogo (US04-Aldair, con `ServicioFormViewModel`).
- Vehículos (US03-Adrian): validación como atributos del `VehiculoFormViewModel` (placa alfanumérica 6-8 sin guiones/símbolos, modelo obligatorio con límite, kilometraje no negativo, observaciones con máximo). Placa normalizada a mayúsculas, duplicado ignorando mayúsculas/minúsculas.

## ViewModels/ (DTOs binding)

- `ViewModels/MecanicoInputModel.cs`: `Ci,NombreCompleto,Especialidad,Celular` plano (sin DataAnnotations).
- `ViewModels/ServicioFormViewModel.cs`: `[Required]` Nombre/Descripcion, `[Range(0.01,double.MaxValue)]` Costo/Tiempo.
- `ViewModels/ServicioTablaViewModel.cs`: DTO para la tabla/partials de servicios (US04-Aldair).
- `ViewModels/VehiculoFormViewModel.cs`: `Placa,Modelo,Kilometraje,Observaciones` + validaciones como atributos (US03-Adrian).
- `ViewModels/DashboardViewModel.cs`: `MecanicosDisponibles, VehiculosRegistrados, ServiciosRegistrados:int`.

## Pages/**/*.cshtml.cs

- `Pages/Index.cshtml.cs`: `OnGet() -> DashboardService.GetDashboardData()`.
- `Pages/Mecanicos/Index.cshtml.cs`: CRUD por modales/handlers. `OnGetAsync()->ObtenerAsync(TerminoBusqueda)`, `OnPostCrearAsync()->CrearAsync`, `OnPostActualizarAsync()->ActualizarAsync`, `OnPostEliminarAsync()->EliminarAsync`. Usa `TempData MensajeExito/Error`, `FormularioActivo=crear/editar`.
- `Pages/Servicios/Index.cshtml.cs`: `OnGet()->ObtenerTodos()`. Vistas independientes `Create/Edit/Delete` + `Control/Registros` y partials `_CamposServicio/_TablaServicios` (US04-Aldair).
- `Pages/Servicios/Create/Edit/Delete.cshtml.cs`: `OnGet(id)`, `OnPost()->Crear/Actualizar/Eliminar()` con `ModelState.IsValid` + trim.
- `Pages/Vehiculos/Index.cshtml(.cs)`: CRUD en 1 pantalla con 3 modales (registrar/editar/eliminar), buscador por placa o modelo, orden alfabético por placa (US03-Adrian). Dep: `IVehiculoService`.
- `Pages/Historial/Index.cshtml.cs`: `OnGet()->ObtenerHistorial()`.
- `Privacy, Error`: plantilla default.

## Frontend (`wwwroot/` + `Pages/Shared/_Layout.cshtml`)

- `css/site.css, mecanicos.css`, `js/site.js, mecanicos.js`, `lib/bootstrap,jquery,jquery-validation(-unobtrusive)`, `favicon.ico`, `images/hero-taller.jpg`.
- `Pages/Servicios/_CamposServicio.cshtml, _TablaServicios.cshtml`: partials para no duplicar formulario/tabla (US04-Aldair).
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
2. Servicios sigue el mismo patrón con interfaz: `ServicioFormViewModel + ValidacionServicios -> ServicioService -> IServicioRepository` (US04-Aldair).
3. No usar EF Core. Todo SQL parametrizado directo.
4. Histórico es automático por trigger, no meter lógica C# para eso.
5. Vehículos replica el patrón con validación en el ViewModel: `VehiculoFormViewModel -> IVehiculoService -> VehiculoService -> IVehiculoRepository` (US03-Adrian).

## Aportes por integrante (resúmenes del equipo, 10/09/2026 – insumo del informe Anexo 1)

### US01 – Dashboard principal – Ariel
- Objetivo: Dashboard con métricas (mecánicos disponibles, vehículos registrados, servicios registrados) + menú de navegación por módulos funcionales.
- Clases: `IndexModel, DashboardService, DashboardViewModel, ServicioRepository, DatabaseConnection` (presentación / lógica / acceso a datos separados).
- SOLID: SRP (una función por clase) + inyección de dependencias para bajo acoplamiento.
- Dificultad: SQL Server LocalDB no disponible en el entorno → se usó SQLite.
- Nota: Ariel hizo cambios sobre el dashboard base (por eso hoy `DashboardService` usa `IServicioRepository + IVehiculoRepository` con `Vehiculos/Servicios` reales y solo `Mecanicos=0` hardcodeado). Rama: `dev1/setup-dashboard`.

### US02 – CRUD de Mecánicos – Santiago
- Entidad: `Id` (PK interna), `CI` (único), `NombreCompleto, Especialidad, Celular`.
- Clases: `Mecanico.cs` (entidad), `MecanicoInputModel.cs` (formulario), `ValidacionMecanicos.cs` (validaciones), `IMecanicoRepository.cs` + `MecanicoRepository.cs` (CRUD), `MecanicoService.cs` (normalización + validación + repositorio), `DatabaseInitializer.cs` (tabla `Mecanicos`), `Pages/Mecanicos/Index.cshtml(.cs)` (página + interfaz).
- Flujo: Vista Razor → IndexModel → MecanicoService → ValidacionMecanicos → IMecanicoRepository → MecanicoRepository → BD.
- Validaciones: CI base 5-8 dígitos + complemento opcional (ej. `1234567-1A`); celular 8 dígitos iniciando en 6|7; nombre/especialidad obligatorios con límite y sin números; limpieza de espacios (inicio/fin/repetidos) antes de validar/guardar; CI duplicado.
- SOLID/Clean Code/POO: SRP (validación / servicio / datos / presentación separados), DIP (`MecanicoService` depende de `IMecanicoRepository`); nombres claros, métodos pequeños, sin duplicación; clases separadas, encapsulación, interfaces + DI.
- Dificultad: devolver varios errores de distintos campos sin mezclar lógica → resuelto con `Dictionary<campo,mensaje>` centralizado en `ValidacionMecanicos`, cada error se muestra en su campo. Rama: `dev2/crud-mecanicos`.

### US03 – CRUD de Vehículos – Adrian
- Como recepcionista, registrar vehículos: `Id` (PK), `Placa` (única), `Modelo, Kilometraje, Observaciones`. Todo en 1 pantalla con 3 modales (registrar/editar/eliminar, diseño Figma) + buscador por placa o modelo. Conectó el contador de vehículos del panel (estaba fijo en cero).
- Clases: `Vehiculo.cs`, `VehiculoFormViewModel.cs` (formulario + validaciones), `IVehiculoRepository.cs` + `VehiculoRepository.cs` (ADO.NET parametrizado), `IVehiculoService.cs` + `VehiculoService.cs` (normalización de placa + repositorio), `DatabaseInitializer.cs` (tabla `Vehiculos`, placa `UNIQUE`), `Pages/Vehiculos/Index.cshtml(.cs)`.
- Flujo: Vista Razor → IndexModel → IVehiculoService → VehiculoService → IVehiculoRepository → VehiculoRepository → BD.
- Validaciones: placa obligatoria alfanumérica 6-8 sin guiones/símbolos (campo en rojo + mensaje naranja "Formato alfanumérico requerido" mientras se escribe); placa normalizada [texto original incompleto: "automáticamente ao esté duplicada, ignorando mayúsculas y minúsculas"]; modelo obligatorio con límite; kilometraje no negativo; observaciones con máximo [texto original incompleto]; si falla, el modal se reabre conservando datos y mostrando el error en su campo.
- SOLID/Clean Code: SRP + DIP (IndexModel→`IVehiculoService`, Service→`IVehiculoRepository`, todo registrado en `Program.cs`; el merge que cambió `ServicioRepository`→`IServicioRepository` no afectó a Vehículos); interfaces pequeñas (ISP) y OCP con reglas como atributos del ViewModel; nombres descriptivos y reutilización de estilos sin duplicar CSS.
- Pruebas: registro/consulta/edición/eliminación, orden alfabético por placa, filtrado, placa inválida y duplicada sin error 500, Servicios/Mecánicos/Historial no afectados por estilos nuevos, `dotnet build` sin errores. Rama: `feature/us03-crud-vehiculos` (mergeada a `main`, PR #11).

### US04 – Catálogo de Servicios – Aldair
- CRUD de Servicios: `Nombre, Descripción, Costo, Tiempo estimado`. Stack Razor Pages + C# + ADO.NET + SQLite, verificado en código: `IServicioRepository`, `ValidacionServicios`, partials `_CamposServicio/_TablaServicios`, `ServicioTablaViewModel`, páginas `Control/Registros`.
- Flujo: Razor Pages → PageModels → ServicioService → IServicioRepository → ServicioRepository → SQLite.
- Validaciones (`ServicioFormViewModel` + `ValidacionServicios` + `ServicioService` + checks SQLite): nombre/descripción obligatorios, costo>0, tiempo>0, sin espacios inválidos ni caracteres no permitidos. Listado `ORDER BY Nombre`. Vistas independientes de registro/consulta/administración.
- SOLID/Clean Code: `IServicioRepository` desacopla negocio de implementación (SRP, ISP, DIP); nombres descriptivos, métodos pequeños, partials contra duplicación.
- Rama `dev4/crud-servicios`, commit `fd67bd6`, PR #10 mergeado a `main`; build final integrado 0 errores.

### US05 – Historial de Costos – Alex
- Como administrador, visualizar el historial de modificaciones del costo de servicios para auditar precios.
- Cada cambio de costo registra: costo anterior, costo nuevo, nombre del servicio y fecha. Tabla `HistorialCostoServicios` + trigger sobre `Costo` de `Servicios` (solo inserta si anterior≠nuevo).
- Capas: `HistorialCostoServicio` (modelo), `IHistorialCostoServicioRepository` + `HistorialCostoServicioRepository` (ADO.NET), `IHistorialCostoServicioService` + `HistorialCostoServicioService`, Razor Page dedicada, opción Historial en menú Operaciones del Taller.
- SOLID/Clean Code: SRP/ISP/DIP (modelo/repositorio/servicio/presentación separados, DI en `Program.cs`); nombres y SQL legibles, mapeo separado.
- Pruebas: 100→125 Bs. registra, 125→150 Bs. registra, solo descripción no registra, refresco no duplica, orden más reciente primero, `dotnet build` sin errores. Estado: completada, probada e integrada a `main` por PR. Rama: `feature/us05-historial-costo-servicios`.
