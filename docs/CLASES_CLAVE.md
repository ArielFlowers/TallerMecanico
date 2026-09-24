# Clases Clave – TallerMecanico

> Memoria técnica para futuras sesiones. No tocar código de compañeros sin avisar.
> Stack: ASP.NET Core Razor Pages .NET 10 + ADO.NET (`MySqlConnector` 2.6.2) + MySQL 8 (ver `README.md`: `docker compose up -d`, BD `taller_mecanico`). Patrón Factory Method en `Data/` + creadores en `Patterns/FactoryMethod/`. Rama: `feature/mysql-factory-method`.
> No hay backend/frontend separados. Todo es monolito: `Pages/*.cshtml` + `wwwroot/`.

## Cómo correr (verificado 24/09/2026, sprint 2 MySQL+Factory)

```powershell
# SDK 10 requerido. Si solo hay SDK 9, instalar user-local:
# & "$env:TEMP\dotnet-install.ps1" -Channel 10.0 -Quality GA -InstallDir "$env:LOCALAPPDATA\Microsoft\dotnet10"
$env:PATH = "$env:LOCALAPPDATA\Microsoft\dotnet10;$env:PATH"
$env:DOTNET_ROOT = "$env:LOCALAPPDATA\Microsoft\dotnet10"

dotnet restore
dotnet build
dotnet watch run   # http://localhost:5196 | https://localhost:7043
```

- `appsettings.json`: `ConnectionStrings:MySqlConnection` (ver `docker-compose.yml`).
- MySQL local del equipo: servidor en `127.0.0.1:3306`, BD `taller_mecanico`, usuario `taller`/`taller123` (root local: `root`/`root`). Ver con Workbench o `mysql.exe -h 127.0.0.1 -u taller -ptaller123 taller_mecanico`.
- Si el arranque falla con "SUPER privilege and binary logging": como root `SET GLOBAL log_bin_trust_function_creators = 1;` (el compose ya trae el flag).
- La DB se autocrea al arrancar vía `Program.cs -> DatabaseInitializer.Initialize()` (4 tablas + trigger MySQL).
- Reset DB = `docker compose down -v` (borra el volumen) y reiniciar. En local sin Docker: `TRUNCATE` x4 (no borra trigger).
- Puertos en `Properties/launchSettings.json`.

## Arquitectura / Flujo

```
Program.cs (DI: DatabaseConnectionFactory->MySql + 3 creadores)
 -> Pages/*.cshtml.cs (PageModel OnGet/OnPost sync)
 -> Services/* (reciben CreadorX, llaman CrearRepositorio())
 -> Patterns/FactoryMethod/CreadorX -> Data/*Repository : IRepository<T> (SQL parametrizado)
 -> MySQL 8 (taller_mecanico)
 -> Razor (.cshtml) + ViewModel
 + ConfiguracionTaller (Singleton clásico, nombre/versión/moneda/fecha)
```

Rutas: `/` Inicio/Dashboard, `/Mecanicos` (CRUD en 1 página con handlers), `/Vehiculos` (CRUD en 1 página con modales), `/Servicios`, `/Servicios/Create`, `/Servicios/Edit?id=`, `/Servicios/Delete?id=`, `/Servicios/Control`, `/Servicios/Registros`, `/Historial`, `/Privacy`, `/Error`. Ramas: `dev1/setup-dashboard` (US01), `dev2/crud-mecanicos` (US02), `feature/us03-crud-vehiculos` (US03), `dev4/crud-servicios` (US04), `feature/us05-historial-costo-servicios` (US05), `feature/home-inicio` (Home, mergeada a `main` vía PR #12), `feature/mysql-factory-method` (sprint 2, actual).

## Models/ (POCOs sin lógica)

| Archivo | Props |
|---|---|
| `Models/Mecanico.cs` | `Id:int, Ci:string (UNIQUE), Nombres:string, Apellidos:string, Genero:string, Especialidad:string, Celular:string` |
| `Models/Servicio.cs` | `Id:int, Nombre:string, Descripcion:string, Costo:decimal, TiempoEstimadoHoras:decimal` |
| `Models/Vehiculo.cs` | `Id:int, Placa:string (UNIQUE), Marca:string, Modelo:string, Kilometraje:int, Observaciones:string` |
| `Models/HistorialCostoServicio.cs` | `Id:int, ServicioId:int, NombreServicio:string, CostoAnterior:decimal, CostoNuevo:decimal, FechaCambio:DateTime` |

## Data/ (acceso MySQL vía factory)

| Archivo | Responsabilidad / Métodos |
|---|---|
| `Data/Factories/DatabaseConnectionFactory.cs` | Abstracta. Lee `MySqlConnection`, `CreateConnection(): DbConnection`. Dep: `IConfiguration`. |
| `Data/Factories/MySqlConnectionFactory.cs` | Concreta. Retorna `MySqlConnection`. Registrada en `Program.cs`. |
| `Data/DatabaseInitializer.cs` | Migración al arranque sobre la factory. Crea `Mecanicos/Servicios/Vehiculos/HistorialCostoServicios` + trigger `TRG_Servicios_HistorialCosto` (vía `INFORMATION_SCHEMA`). Todo `IF NOT EXISTS`. |
| `Patterns/FactoryMethod/CreadorRepositorio.cs` | Creador genérico abstracto `CreadorRepositorio<T>` (clon del `CreatorCRUD<T>` del ejemplo Demo2 del docente): solo `CrearRepositorio(): IRepository<T>`. Sin `SomeOperation` por decisión (el ejemplo no lo trae). |
| `Patterns/FactoryMethod/CreadorMecanico/CreadorVehiculo/CreadorServicio.cs` | Creadores concretos. Reciben `DatabaseConnectionFactory` (conexión fuera del patrón, como exige la rúbrica), cada uno retorna exclusivamente su repo como `IRepository<T>` (igual que el ejemplo). |
| `Data/MecanicoRepository.cs` : `IRepository<Mecanico>` | CRUD sync + extras `Search(termino)`, `ExistsByCi(ci,idExcluido)`. `LIKE ... ESCAPE '!'`, `=` con collation CI de MySQL. |
| `Data/ServicioRepository.cs` : `IRepository<Servicio>` | CRUD. `GetAll(), GetById(id), Add(), Update(), Delete(), Count()`, helpers `AddParameters(), MapServicio()`. Listado `ORDER BY Nombre`. |
| `Data/VehiculoRepository.cs` : `IRepository<Vehiculo>` | CRUD + extras `Search(filtro)`, `ExistsByPlaca(placa,idExcluido)`. `Count()` para el Dashboard. |
| `Data/HistorialCostoServicioRepository.cs` + `IHistorialCostoServicioRepository.cs` | Solo lectura (fuera del Factory por rúbrica). `GetAll()->IReadOnlyList` orden `FechaCambio DESC, Id DESC`. |

Tablas (MySQL 8, `DEFAULT CHARSET=utf8mb4`, DDL en `Data/Scripts/MySql/`):
- `Mecanicos(Id INT AI PK, Ci VARCHAR(20) UNIQUE NOT NULL, Nombres, Apellidos, Genero + CHECK, Especialidad + CHECK lista, Celular)`.
- `Servicios(Id INT AI PK, Nombre, Descripcion, Costo DECIMAL(10,2) CHECK>0, TiempoEstimadoHoras DECIMAL(6,2) CHECK>0)`.
- `Vehiculos(Id INT AI PK, Placa VARCHAR(10) UNIQUE NOT NULL, Marca DEFAULT '', Modelo, Kilometraje INT CHECK>=0, Observaciones DEFAULT '')`.
- `HistorialCostoServicios(Id INT AI PK, ServicioId INT, NombreServicio, CostoAnterior DECIMAL, CostoNuevo DECIMAL, FechaCambio DATETIME)`.
- Trigger `TRG_Servicios_HistorialCosto`: `AFTER UPDATE ON Servicios FOR EACH ROW IF OLD.Costo<>NEW.Costo INSERT ... NOW()`. MySQL no tiene `CREATE TRIGGER IF NOT EXISTS`: se verifica vía `INFORMATION_SCHEMA.TRIGGERS`. Comparación `=` ya es case-insensitive por collation (reemplaza `COLLATE NOCASE`); `LIKE` usa `ESCAPE '!'`.
- Eliminados en sprint 2: `Data/DatabaseConnection.cs` (SQLite), `IMecanicoRepository.cs`, `IVehiculoRepository.cs`, interfaz marcadora `IRepository` vacía, paquete `Microsoft.Data.Sqlite`, `TallerMecanico.db`.

## Services/ (regla de negocio, `Scoped` en `Program.cs`)

| Archivo | Métodos / Notas |
|---|---|
| `Services/MecanicoService.cs` | `Crear(Input)->Errores`, `Obtener(termino?)`, `Actualizar(id,Input)->(bool,Errores)`, `Eliminar(id)->bool` (sync). Normaliza: trim, colapsa `\s+`, CI complemento a mayúsculas. Solo chequea CI duplicado si pasa `ValidacionMecanicos`. Dep: `CreadorMecanico` (cast a `MecanicoRepository` una vez en el ctor para `Search/ExistsByCi`, exigido por retorno-interfaz del ejemplo) `+ ValidacionMecanicos`. |
| `Services/ServicioService.cs` | `ObtenerTodos(), ObtenerPorId(), Crear(), Actualizar(), Eliminar()`. Valida vía `ServicioFormViewModel` + `ValidacionServicios` + checks (nombre/descripción obligatorios, `Costo/Tiempo>0`). Dep: `CreadorServicio` (guarda `IRepository<Servicio>`, sin cast). |
| `Services/VehiculoService.cs` | Coordina normalización de placa (mayúsculas) + validación + repo. Duplicado de placa: pre-chequeo `ExistsByPlaca` + red `MySqlException` 1062. Dep: `CreadorVehiculo` (cast a `VehiculoRepository` una vez en el ctor) `+ ValidacionVehiculos`. |
| `Services/ConfiguracionTaller.cs` | **Singleton clásico GoF** (puntos extra, fuera del Factory): `sealed`, ctor privado, `Lazy<T>` thread-safe. `NombreTaller/Version/Moneda/FormatoFecha`. Consumido en `Pages/Index.cshtml` (footer+fecha) y `Pages/Historial/Index.cshtml` (moneda). `new` solo dentro de la clase. |
| `Services/DashboardService.cs` | `GetDashboardData()->DashboardViewModel`. `Vehiculos/Servicios=Count()` reales; `Mecanicos=0` sigue hardcodeado (pendiente conectar). Dep: `IRepository<Servicio> + IRepository<Vehiculo>` (resueltos vía creadores en `Program.cs`). |
| `Services/HistorialCostoServicioService.cs` + `IHistorialCostoServicioService.cs` | Fachada fina. `ObtenerHistorial()` → repo. |

## Validators/

- `Validators/ValidacionMecanicos.cs` – puro, retorna `Dictionary<campo,msg>`.
  - `Validar(Input)` → `ValidarCi(), ValidarNombres(), ValidarApellidos(), ValidarCelular()`. (Genero/Especialidad NO se validan en C#: el form usa `<select>` y la BD tiene CHECK; hueco conocido de bajo riesgo.)
  - CI: `5-8 dígitos + opcional -complemento 1-2 alfanumérico`.
  - Nombres/Apellidos `<=100`, solo letras y espacios, obligatorios.
  - Celular `==8 dígitos, solo [0-9], inicia 6|7`.
- `Validators/ValidacionServicios.cs` – validación del catálogo (US04-Aldair, con `ServicioFormViewModel`).
- Vehiculos (US03-Adrian): `ValidacionVehiculos` normaliza y valida placa (3 o 4 numeros + 3 letras), marca/modelo del catalogo, kilometraje no negativo y observaciones (250 caracteres). Servicio concreto y repositorio con interfaz; duplicados excluyen el Id editado.

## ViewModels/ (DTOs binding)

- `ViewModels/MecanicoInputModel.cs`: `Ci,Nombres,Apellidos,Genero,Especialidad,Celular` plano (sin DataAnnotations).
- `ViewModels/ServicioFormViewModel.cs`: `[Required]` Nombre/Descripcion, `[Range(0.01,double.MaxValue)]` Costo/Tiempo.
- `ViewModels/ServicioTablaViewModel.cs`: DTO para la tabla/partials de servicios (US04-Aldair).
- `ViewModels/VehiculoFormViewModel.cs`: entrada `Id,Placa,Marca,Modelo,Kilometraje,Observaciones`; reglas en `ValidacionVehiculos`.
- `ViewModels/DashboardViewModel.cs`: `MecanicosDisponibles, VehiculosRegistrados, ServiciosRegistrados:int`.

## Pages/**/*.cshtml.cs

- `Pages/Index.cshtml.cs`: `OnGet() -> DashboardService.GetDashboardData()`.
- `Pages/Mecanicos/Index.cshtml.cs`: CRUD por handlers sync. `OnGet()->Obtener(TerminoBusqueda)`, `OnPostCrear()->Crear`, `OnPostActualizar()->Actualizar`, `OnPostEliminar()->Eliminar`. Usa `TempData MensajeExito/Error`, `FormularioActivo=crear/editar`.
- `Pages/Servicios/Index.cshtml.cs`: `OnGet()->ObtenerTodos()`. Vistas independientes `Create/Edit/Delete` + `Control/Registros` y partials `_CamposServicio/_TablaServicios` (US04-Aldair).
- `Pages/Servicios/Create/Edit/Delete.cshtml.cs`: `OnGet(id)`, `OnPost()->Crear/Actualizar/Eliminar()` con `ModelState.IsValid` + trim.
- `Pages/Vehiculos/Index.cshtml(.cs)`: CRUD en 1 pantalla con 3 modales (registrar/editar/eliminar), buscador por placa o modelo, orden alfabético por placa (US03-Adrian). Dep: `VehiculoService`.
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

1. Mecánicos = patrón ideal: `InputModel -> ValidacionMecanicos -> MecanicoService (normaliza) -> CreadorMecanico.CrearRepositorio() -> MecanicoRepository : IRepository<Mecanico>`.
2. Servicios y Vehículos igual: `Service -> CreadorX.CrearRepositorio() -> Repo : IRepository<T>`. Extras (`Search`, `ExistsBy*`) en la clase concreta; Services los usan vía cast único en el ctor (impuesto por el retorno-interfaz del ejemplo Demo2).
3. No usar EF Core. Todo SQL parametrizado directo vía `DatabaseConnectionFactory` (MySQL).
4. Histórico es automático por trigger, no meter lógica C# para eso.
5. Vehiculos: `VehiculoFormViewModel -> VehiculoService -> ValidacionVehiculos / CreadorVehiculo`. El servidor valida antes de persistir; la pagina muestra errores por campo.

## Sprint 2 – MySQL + Factory Method + Singleton (`feature/mysql-factory-method`, 24/09/2026)

> Commits: `72f829b` (factory+mysql) y `bdbf2dc` (singleton). Auditoría del ejemplo Demo2 del docente (`Arquitectura De software/Ejemplos/FactoryMethod/Demo2`): nuestro `IRepository<T>` ≈ su `ICRUD<T>`, `CreadorRepositorio<T>` ≈ su `CreatorCRUD<T>` (sin `SomeOperation`: ni su ejemplo ni la rúbrica oficial lo piden), hijos retornando la interfaz, `new Repo(` solo en creadores (verificado por grep).

- **MySQL**: `docker-compose.yml` (mysql:8, BD `taller_mecanico`, `taller`/`taller123`) + `MySqlConnection` en `appsettings.json`. `ServicioRepository` ya era agnóstico (`DbConnection`); se migraron `Vehiculo/Historial/Mecanico` + `DatabaseInitializer` (4 tablas + trigger, `INFORMATION_SCHEMA`, `NOW()`, `AUTO_INCREMENT`).
- **Mecánicos reescrito a sync inglés** (`IRepository<Mecanico>` + `Search/ExistsByCi`), `MecanicoService` sync, `Pages/Mecanicos` handlers sync. Valida igual que antes.
- **Incidente real y fix**: crear triggers exige `SUPER` con binlog → `SET GLOBAL log_bin_trust_function_creators=1` + flag en el compose + nota en `README.md`.
- **Verificación E2E contra MySQL 8.0 local** (25+ casos en verde, luego BD reseteada a 0 filas): CRUD x3, trigger (descripción sola→0, costo 100→125→`100.00->125.00`), duplicados CI/placa con mensaje y sin 500, validaciones, ordenamientos (`Apellidos/Nombre/Placa`), búsquedas, `/Control`, `/Registros`, Dashboard 200. Placa exige formato boliviano `^[0-9]{3,4}[A-Z]{3}$`; `/Servicios` es menú, la lista vive en `/Registros`.
- **Singleton puntos extra**: `Services/ConfiguracionTaller.cs` (sealed, ctor privado, `Lazy<T>`), consumido en footer/fecha (`Index`) y moneda (`Historial`). UI idéntica.
- **Auditoría 24/09**: factory 15/15, histórico 7/7, validaciones 18/18, CRUDs ~28-30/30. Pendientes NO código: informe Anexo 1 (10 pts, no existe PDF), logo emoji + `href="#"` muertos (pto. Home), `MecanicosDisponibles=0` hardcodeado, `MecanicoService` sin red 1062 ante carrera de CI duplicado.

## Aportes por integrante (resúmenes del equipo, 10/09/2026 – insumo del informe Anexo 1)

### US01 – Dashboard principal – Ariel
- Objetivo: Dashboard con métricas (mecánicos disponibles, vehículos registrados, servicios registrados) + menú de navegación por módulos funcionales.
- Clases: `IndexModel, DashboardService, DashboardViewModel, ServicioRepository, DatabaseConnection` (presentación / lógica / acceso a datos separados).
- SOLID: SRP (una función por clase) + inyección de dependencias para bajo acoplamiento.
- Dificultad: SQL Server LocalDB no disponible en el entorno → se usó SQLite.
- Nota: Ariel hizo cambios sobre el dashboard base (por eso hoy `DashboardService` usa `IServicioRepository + IVehiculoRepository` con `Vehiculos/Servicios` reales y solo `Mecanicos=0` hardcodeado). Rama: `dev1/setup-dashboard`.

### US02 – CRUD de Mecánicos – Santiago
- Entidad: `Id` (PK interna), `CI` (único), `NombreCompleto, Especialidad, Celular`.
- Clases: `Mecanico.cs` (entidad), `MecanicoInputModel.cs` (formulario), `ValidacionMecanicos.cs` (validaciones), `MecanicoRepository.cs` (CRUD), `MecanicoService.cs` (normalización + validación + repositorio), `DatabaseInitializer.cs` (tabla `Mecanicos`), `Pages/Mecanicos/Index.cshtml(.cs)` (página + interfaz).
- Flujo original: Vista Razor → IndexModel → MecanicoService → ValidacionMecanicos → IMecanicoRepository → MecanicoRepository → BD (SQLite). **Sprint 2: `IMecanicoRepository` eliminada, repo sync `IRepository<Mecanico>` vía `CreadorMecanico`, todo a MySQL (ver sección Sprint 2).**
- SOLID/Clean Code/POO: SRP (validación / servicio / datos / presentación separados), DIP (`MecanicoService` depende del creador que retorna `IRepository<Mecanico>`); nombres claros, métodos pequeños, sin duplicación; clases separadas, encapsulación, interfaces + DI.
- Dificultad: devolver varios errores de distintos campos sin mezclar lógica → resuelto con `Dictionary<campo,mensaje>` centralizado en `ValidacionMecanicos`, cada error se muestra en su campo. Rama: `dev2/crud-mecanicos`.

### US03 - CRUD de Vehiculos - Adrian
- Campos: `Id`, `Placa` unica, `Marca`, `Modelo`, `Kilometraje`, `Observaciones`. Tres modales y busqueda por placa, marca o modelo.
- Flujo original: Razor Page -> `VehiculoService` concreto -> `ValidacionVehiculos` y `IVehiculoRepository` -> SQLite. **Sprint 2: `IVehiculoRepository` eliminada, `VehiculoRepository : IRepository<Vehiculo>` vía `CreadorVehiculo`, todo a MySQL.** Se elimina la interfaz del servicio siguiendo el patron de los otros CRUD.
- `ValidacionVehiculos` limpia espacios y capitalizacion antes de comprobar las reglas. Placa: 3 o 4 numeros y exactamente 3 letras ASCII, guardada en mayusculas. Marca/modelo: nombres canonicos de `CatalogoVehiculos`, sin aceptar combinaciones inexistentes. Observaciones: trim y espacios repetidos reducidos, preservando saltos de linea.
- Catalogo fijo compartido entre servidor y desplegables Marca -> Modelo. Ampliable en `Models/CatalogoVehiculos.cs`. Modelo maximo 60, observaciones 250, kilometraje no negativo y vacio equivalente a cero.
- El servicio comprueba duplicados excluyendo el vehiculo editado y devuelve errores por campo. Los errores de conversion numerica impiden guardar. Los modales conservan las selecciones tras errores.
- Factory Method actual: `CreadorVehiculo : CreadorRepositorio<Vehiculo>` construye `VehiculoRepository` mediante `DatabaseConnectionFactory`. El repositorio implementa `IRepository<Vehiculo>` y conserva `Search` y `ExistsByPlaca` como métodos concretos.
- Inyección: `Program.cs` registra `CreadorVehiculo` e `IRepository<Vehiculo>` con duración `Scoped`. `VehiculoService` recibe el creador y convierte su resultado a `VehiculoRepository`; el dashboard recibe la interfaz genérica.
- Persistencia actual: MySQL, con `Marca` incluida en la creación de la tabla. La migración de columna SQLite correspondía al sprint anterior; no es una migración de datos entre motores.
- SRP: presentación, reglas, coordinación y persistencia separadas. El servicio aún depende del repositorio concreto para sus operaciones específicas.
- Verificación: `dotnet build`. Los scripts de vehículos de `tests/` corresponden a la etapa SQLite y deben adaptarse antes de ejecutarse sobre esta rama MySQL; ver `tests/README.md`.

### US04 – Catálogo de Servicios – Aldair
- CRUD de Servicios: `Nombre, Descripción, Costo, Tiempo estimado`. Stack Razor Pages + C# + ADO.NET (SQLite en su sprint; **MySQL desde sprint 2**), verificado en código: `ValidacionServicios`, partials `_CamposServicio/_TablaServicios`, `ServicioTablaViewModel`, páginas `Control/Registros`.
- Flujo original: Razor Pages → PageModels → ServicioService → IServicioRepository → ServicioRepository → SQLite. **Sprint 2: `ServicioService` recibe `CreadorServicio` (`IRepository<Servicio>`), repo sobre `DatabaseConnectionFactory` MySQL.**
- Validaciones (`ServicioFormViewModel` + `ValidacionServicios` + `ServicioService` + checks de BD de su sprint): nombre/descripción obligatorios, costo>0, tiempo>0, sin espacios inválidos ni caracteres no permitidos. Listado `ORDER BY Nombre`. Vistas independientes de registro/consulta/administración.
- SOLID/Clean Code: `IServicioRepository` desacopla negocio de implementación (SRP, ISP, DIP); nombres descriptivos, métodos pequeños, partials contra duplicación.
- Rama `dev4/crud-servicios`, commit `fd67bd6`, PR #10 mergeado a `main`; build final integrado 0 errores.

### US05 – Historial de Costos – Alex
- Como administrador, visualizar el historial de modificaciones del costo de servicios para auditar precios.
- Cada cambio de costo registra: costo anterior, costo nuevo, nombre del servicio y fecha. Tabla `HistorialCostoServicios` + trigger sobre `Costo` de `Servicios` (solo inserta si anterior≠nuevo).
- Capas: `HistorialCostoServicio` (modelo), `IHistorialCostoServicioRepository` + `HistorialCostoServicioRepository` (ADO.NET), `IHistorialCostoServicioService` + `HistorialCostoServicioService`, Razor Page dedicada, opción Historial en menú Operaciones del Taller.
- SOLID/Clean Code: SRP/ISP/DIP (modelo/repositorio/servicio/presentación separados, DI en `Program.cs`); nombres y SQL legibles, mapeo separado.
- Pruebas: 100→125 Bs. registra, 125→150 Bs. registra, solo descripción no registra, refresco no duplica, orden más reciente primero, `dotnet build` sin errores. Estado: completada, probada e integrada a `main` por PR. Rama: `feature/us05-historial-costo-servicios`.
