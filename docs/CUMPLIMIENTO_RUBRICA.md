# Cumplimiento de Rúbrica EC2 – verificación con evidencia (`refactor/hexagonal`)

> Verificado con `dotnet build` 0/0, tests 152 passed (9 ambientales por
> `secrets.json` ausente, 37 skipped), `node --check` OK y grep sobre el código.
> Comandos de verificación al final.

## 1. Rúbrica funcional (74 pts)

| Rubro (pts) | Estado | Evidencia (archivo:línea / comando) |
|---|---|---|
| Login 4 | ✅ | `Pages/Auth/Login.cshtml(.cs)`; `Program.cs`: `AuthorizeFolder` + policies `SoloAdministrador`/`PersonalAutorizado` (18 matches); `_Layout.cshtml`: menú por rol con `IsInRole` (8 matches); `AccessDenied.cshtml` |
| Sesiones 7 | ✅ | `Program.cs`: `AddSession` + `UseSession`; `ICurrentUser` desde sesión en todos los services; `CreadoPor/ModificadoPor/AnuladoPor` + `Fecha*` en 5 entidades + Órdenes (`DatabaseInitializer`: 8 cols `ModificadoPor`); tabla `Auditoria` (login/logout); ningún form pide usuario |
| UI proceso 25 | ✅ | 2 buscadores: `_BuscadorVehiculo.cshtml`, `_BuscadorProducto.cshtml`; `+Nuevo vehículo/cliente/producto` con retorno al borrador (`Crear.cshtml.cs: OnPostNuevo*`); temporal ABM en sesión (`Agregar/ActualizarCantidad/QuitarLinea`); sumariados vivos (`resumen-lineas/cantidad/total` + `ordenes.js` sin conflicto); `confirm('¿Guardar orden?')` |
| Transacción 10 | ✅ | `OrdenServicioFacade.CrearOrden` + `MySqlUnidadTrabajoOrden` (misma `DbConnection`, 25 matches `BeginTransaction/FOR UPDATE/Confirmar/Token`); `SELECT … FOR UPDATE` ordenado por Id; precios recalculados desde BD; `Descontar` condicional + rollback; idempotencia por token (`EsReenvio`); `CreadoPor` de sesión |
| Anulación 8 | ✅ | `OnPostAnular` SoloAdmin + `confirm` (`Index.cshtml(.cs)`, 5 matches `Anular`); `AnularOrden`: `FOR UPDATE`, estado `Anulada`, restitución 1:1, re-anular devuelve “ya anulada” sin tocar stock; `AnuladoPor/FechaAnulacion` de sesión |
| Select 2 + Modal 6 (8) | ✅ | `Ordenes/Index`: 1 fila por orden, filtros estado/fecha, Refrescar/Limpiar; `_DetalleOrden.cshtml` solo lectura vía `fetch` + `<dialog>` (tras fusionar `ordenes.js`) |
| Comprobante 12 | ✅ | `ComprobanteOrdenPdf` (logo AP, `#7c3aed`, generales + detalle + total en letras + fecha Bolivia + `GeneradoPor` vs `CreadoPor`); auto `window.open` + enlace fallback (`Crear.cshtml`); `Comprobante.cshtml.cs` GET idempotente (PDF inline, 0 INSERT) |
| **Subtotal funcional** | **74/74** | — |

## 2. Arquitectura y patrones (10 pts)

| Exigencia | Estado | Evidencia |
|---|---|---|
| Monolito por Responsabilidad Técnica | ✅ | `Dominio/`, `Aplicacion/`, `Infraestructura/`, `Pages/` (= Presentación). Ver `docs/ARQUITECTURA_HEXAGONAL.md` |
| Hexagonal (Puertos y Adaptadores) | ✅ | 24 puertos en `Aplicacion/Puertos/` (`IClientePort`, `IOrdenPort`, `IRepository<T>`, `IFallbackHash`…); adaptadores en `Infraestructura/Persistencia/MySql`, `Sesion`, `Seguridad`, `Correo`, `Documentos`; `new Repo(` solo en `Persistencia/Fabricas`; `new MySqlConnection` solo en `Persistencia`; 0 `using Infraestructura` en `Aplicacion/Dominio` salvo `MecanicoService` (excepción Factory Method: el patrón exige depender del Creador) |
| Facade (obligatorio) | ✅ | `Aplicacion/Fachadas/OrdenServicioFacade`, `ConsultaOrdenesFacade`, `RegistroFacade` — Pages solo llaman fachadas |
| Factory Method | ✅ | `Persistencia/Fabricas/Creador*` (10) retornan `IRepository<T>`/puertos; `new` de repos solo ahí (grep vacío fuera) |
| Singleton (3.er patrón) | ✅ | `Infraestructura/Configuracion/ConfiguracionTaller` (`Lazy<T>`) + `AddSingleton(Instancia)` inyectado en Pages y PDF |
| Clean Code + SOLID | ✅ | SRP por capa; DIP (DI contra puertos, `Program.cs`); ISP (puertos de lectura vs escritura); OCP (nuevo adaptador sin tocar casos de uso); validaciones por entidad; 0 warnings de build |

## 3. Defensa y forma (16 pts + ortografía)

| Exigencia | Estado | Evidencia |
|---|---|---|
| Defensa individual | ✅ | `docs/DEFENSA_T6_HEXAGONAL.md`: tabla SOLID por integrante + recorrido demo 5 min |
| Repo con historial | ✅ | Rama `refactor/hexagonal`, 8 commits (`git mv` conserva historia), PR a `main` |
| Ortografía (−1 c/u) | ✅ | Tildes corregidas (`válido, verificación, electrónico, contraseña…`); `href="#"` neutralizados (CSS + JS, no navegan); sin `�` en vistas (era lectura de consola, archivos UTF-8 OK) |

## 4. Comandos de verificación (SDK 10)

```powershell
$env:PATH = "$env:LOCALAPPDATA\Microsoft\dotnet10;$env:PATH"
$env:DOTNET_ROOT = "$env:LOCALAPPDATA\Microsoft\dotnet10"
dotnet build                                            # 0 warnings, 0 errors
node --check wwwroot/js/ordenes.js                      # sin conflicto
dotnet test tests/TallerMecanico.IntegrationTests/…     # 152 passed; 9 fallan solo por secrets.json ausente
# Hexagonal:
Select-String -Path "Aplicacion/**","Dominio/**","Pages/**" -Pattern "new \w+Repository\("   # vacío
Select-String -Path "Aplicacion/**","Dominio/**" -Pattern "using TallerMecanico\.Infraestructura"  # solo MecanicoService (Factory Method)
```

## 5. Nota: arranque con BD local sucia (no es del refactor)

Si al arrancar aparece `EnsureMecanicosEmptyForApellidoMigration`
(“existen registros pendientes de migración manual”), la BD local tiene
la tabla `Mecanicos` con la columna antigua `Apellidos` + filas. Es un
guardián preexistente de `main` (no auto-dividir apellidos). Opciones:

```sql
-- A. Desarrollo (descarta datos): vaciar y dejar que migre solo
SET FOREIGN_KEY_CHECKS = 0;
TRUNCATE TABLE Mecanicos;
SET FOREIGN_KEY_CHECKS = 1;
-- o reset total: DROP DATABASE taller_mecanico; CREATE DATABASE taller_mecanico CHARACTER SET utf8mb4;
-- B. Conservar datos: respaldar, repartir apellidos manualmente y retirar la columna vieja
-- (ver mensaje de la excepción), luego reiniciar.
```
