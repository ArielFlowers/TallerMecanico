# Defensa T6 – Hexagonal + Patrones + Calidad (`refactor/hexagonal`)

> Rama: `refactor/hexagonal` (6 commits sobre `main 478e262`).
> Alcance pactado: rúbrica exacta EC2, sin extras. Sin Strategy, sin Docker de app.

## 1. Mapa de nombres nuevos (qué se movió y por qué)

| Antes | Ahora | Capa |
|---|---|---|
| `Models/*.cs` (14) | `Dominio/Entidades/*.cs` | Dominio: datos + reglas (POCOs) |
| `Services/ImporteEnPalabras.cs` | `Dominio/Servicios/ImporteEnPalabras.cs` | Dominio: lógica pura testeable |
| `Application/Ports/*.cs` (21, mismo nombre `I*Port`) | `Aplicacion/Puertos/*.cs` | Aplicación: contratos hexagonales |
| `Application/Ordenes/*.cs` | `Aplicacion/CasosUso/Ordenes/*.cs` | Aplicación: DTOs del proceso principal |
| `Application/*Facade.cs` (+ `Facades/`) | `Aplicacion/Fachadas/*.cs` (1 namespace) | Aplicación: punto único de entrada |
| `Application/Exceptions/*.cs` | `Aplicacion/Errores/*.cs` | Aplicación: errores de negocio |
| `Services/*Service.cs` | `Aplicacion/Servicios/*Service.cs` | Aplicación: casos de uso |
| `Validators/*.cs` | `Aplicacion/Validaciones/*.cs` | Aplicación: reglas de entrada |
| `ViewModels/*.cs` | `Aplicacion/Dto/*.cs` | Aplicación: binding Razor |
| `Data/*` + `Factories/*` + `Scripts/*` | `Infraestructura/Persistencia/*` | Infraestructura: base de todo lo MySQL |
| `Infraestructura/Adapters/MySql/*.cs` (14) | `Infraestructura/Persistencia/MySql/*.cs` | Infraestructura: adaptadores MySQL |
| `Patterns/FactoryMethod/Creador*.cs` (10) | `Infraestructura/Persistencia/Fabricas/*.cs` | Infraestructura: creación (Factory Method) |
| `Infraestructura/Session/*.cs` | `Infraestructura/Sesion/*.cs` | Infraestructura: borradores + token |
| `Infraestructura/Identity/*.cs` | `Infraestructura/Seguridad/*.cs` | Infraestructura: usuario + hash |
| `Infraestructura/Pdf/*.cs` | `Infraestructura/Documentos/*.cs` | Infraestructura: comprobante PDF |
| `Infraestructura/Setup/*.cs` | `Infraestructura/Arranque/*.cs` | Infraestructura: seed admin |
| `Infraestructura/Adapters/Gmail/*.cs` | `Infraestructura/Correo/*.cs` | Infraestructura: envío de correo |
| `Services/ConfiguracionTaller.cs` | `Infraestructura/Configuracion/*.cs` | Infraestructura: Singleton por DI |
| `Pages/**` | **No se mueve** (rompería rutas Razor) | Se declara como **Presentación** |

Regla de dependencias (defender con el diagrama):
`Presentación → Aplicación → Dominio`. Infraestructura implementa
los puertos. DI solo contra puertos/fachadas. `new Repo(` solo en
Fábricas. `new MySqlConnection` solo en Persistencia.

## 2. Qué se hizo (por commit, en orden)

1. `0c31537` **Parches T1–T5** (auditoría previa):
   `wwwroot/js/ordenes.js` fusionado (tenía marcas `<<<<<<<` que rompían
   filtros + modal); `OnPostAnular` SoloAdmin en `Ordenes/Index` con
   confirm; comprobante auto (`window.open` + enlace fallback, GET
   idempotente); tildes (`válido, verificación, electrónico…`).
2. `760eb79` **Moves puros** (`git mv`, 0 cambios de código): estructura
   por Responsabilidad Técnica.
3. `3299a9b` **Namespaces + Singleton DI**: 189 archivos a
   `TallerMecanico.Dominio|Aplicacion|Infraestructura`; `ConfiguracionTaller`
   registrado `AddSingleton(Instancia)` e inyectado (Pages + PDF).
   `ImporteEnPalabras` a `Dominio.Servicios` (+ `using` en `Program.cs`).
4. `8888b90` **Cierre de rúbrica**: dashboard por rol (conteo real de
   mecánicos, banner auditoría solo Admin); auditoría `CreadoPor` +
   `ModificadoPor` en Clientes, Vehículos, Productos, Servicios y
   Mecánicos (modelos + `DatabaseInitializer` idempotente con
   `ColumnExists` + 5 repos + 5 services con `ICurrentUser` opcional);
   `href="#"` neutralizados (CSS + JS global); este doc + `ARQUITECTURA_HEXAGONAL.md`.
5. `f624d03` + `cc19fcc` **Tests**: adaptados a namespaces/constructores;
   `TempData` nulo-seguro (el NRE que fallaba también blindado en prod).

## 3. Rúbrica EC2: dónde está cada punto (100)

| Rubro (pts) | Evidencia para mostrar |
|---|---|
| Login 4 + Sesiones 7 (11) | `Pages/Auth/*`, `AuthorizeFolder` + policies en `Program.cs`, menú por rol en `_Layout`, `CreadoPor/ModificadoPor/AnuladoPor` desde sesión, tabla `Auditoria`, hash Argon2id |
| UI proceso 25 | `Ordenes/Crear`: 2 filtros, temporal en sesión (`BorradorOrdenSesion`), `+Nuevo` con retorno, sumariados vivos, `confirm('¿Guardar orden?')` |
| Transacción 10 | `OrdenServicioFacade.CrearOrden` + `MySqlUnidadTrabajoOrden` (misma conexión, `FOR UPDATE` ordenado, precios de BD, rollback, idempotencia por token) |
| Anulación 8 | `OnPostAnular` SoloAdmin + `AnularOrden` (estado + restitución 1:1 + “ya anulada”) |
| Select 2 + Modal 6 (8) | `Ordenes/Index` (1 fila/orden, filtros, Refrescar) + `_DetalleOrden` solo lectura vía `fetch` |
| Comprobante 12 | `ComprobanteOrdenPdf` (logo AP, `#7c3aed`, detalle + total en letras + fecha Bolivia + GeneradoPor vs CreadoPor), auto `window.open` + fallback, PDF inline idempotente |
| Patrones/SOLID 10 | Facade (`Aplicacion/Fachadas`), Factory Method (`Persistencia/Fabricas`), Singleton (`Configuracion` por DI); SRP por capa, DIP (puertos), ISP (puertos pequeños), OCP (agregar adaptador sin tocar casos de uso) |
| Defensa 16 | Cada uno: su tarjeta + 1 patrón + 1 SOLID (tabla abajo) |

## 4. Tabla SOLID por integrante (para la pregunta del docente)

| Principio | Ejemplo | Quién lo defiende |
|---|---|---|
| S | `ValidacionClientes` solo valida; `ClienteService` solo coordina; `ClienteRepository` solo SQL | T2 |
| O | Nuevo adaptador (ej. otro motor) implementa `IClientePort` sin tocar servicios | T6 |
| L | Cualquier `IRepository<T>` sustituye a otro en `DashboardService` | T6 |
| I | `IConsultaOrdenesPort` (lectura) vs `IOrdenPort` (escritura); ports de sesión mínimos | T5/T1 |
| D | Services y Pages dependen de `I*Port`/fachadas, nunca de `*Repository` concretos | Todos |

## 5. Verificación (qué decir si preguntan “¿rompieron algo?”)

- App: `dotnet build` **0 warnings, 0 errors** (SDK 10 en `%LOCALAPPDATA%\Microsoft\dotnet10`).
- Tests proyecto: compila **0/0**; unitarios **131 passed, 0 failed** (2 skipped: contenido PDF, requieren `TALLER_PDF_PYTHON`).
- Suite completa con MySQL local: **152 passed, 37 skipped, 9 failed** — los 9 son `*MySqlTests` de auth/registro que exigen `secrets.json`/User Secrets ausentes en este equipo (fallan en `CrearFactory`, antes de tocar código; ver `tests/README.md:58`). **Preexistente, no causado por el refactor** (esa zona solo cambió de namespace).
- SQL 100% parametrizado (`@Param`, `ESCAPE '!'` en LIKE, 0 SQL en Pages); trigger `TRG_Servicios_HistorialCosto` intacto.
- `node --check wwwroot/js/ordenes.js`: OK.

## 6. Cómo levantarlo y qué mirar (demo de 5 min)

```powershell
$env:PATH = "$env:LOCALAPPDATA\Microsoft\dotnet10;$env:PATH"
$env:DOTNET_ROOT = "$env:LOCALAPPDATA\Microsoft\dotnet10"
dotnet build
dotnet run                  # http://localhost:5196 (ver Properties/launchSettings.json)
```

Puertos en `Properties/launchSettings.json`. BD: servicio `MySQL80`
local (`taller`/`taller123`, BD `taller_mecanico`); las tablas + trigger
se autocrean al arrancar (`DatabaseInitializer`).

Recorrido sugerido: login admin vs recep (menú distinto) → `Órdenes/Crear`
(filtros, temporal, sumariados, `+Nuevo`, confirm) → guardar (se abre el
comprobante) → `Órdenes` (Refrescar, Ver modal, Comprobante) → Anular como
admin (restituye stock; re-anular dice “ya anulada”) → `Historial`
(trigger por cambio de costo) → Inicio (dashboard por rol).
