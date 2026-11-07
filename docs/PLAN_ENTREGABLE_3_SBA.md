# Plan Entregable 3 — SBA

> Documento histórico del diagnóstico del 04/11/2026. La decisión posterior es
> ejecutar cuatro aplicaciones independientes, como describe [SBA.md](SBA.md).
> Las alternativas de un solo proyecto y las referencias a réplicas de este plan
> no describen el despliegue vigente.

> Proyecto: TallerMecanico.
> Fecha: 04/11/2026.
> Línea base: `origin/main` (`6a3ce87`).

## 1. Diagnóstico actual

### Servicio 1: casi completo

`main` ya implementa:

- Cuatro buscadores: vehículo, servicio, mecánico y producto.
- Tabla temporal de repuestos y servicios antes de guardar.
- Edición de cantidades, eliminación de líneas y totales dinámicos.
- Creación al vuelo de cliente, vehículo y producto sin perder el borrador.
- Confirmación antes de guardar.
- Transacción con commit y rollback.
- Auditoría de creación y anulación.
- Anulación lógica con restitución de stock.
- Listado general, modal de detalle de solo lectura y comprobante PDF.

Pendiente:

- El listado no tiene un botón explícito de refresco; solo "Limpiar filtros".
- El comprobante se abre en un modal con iframe, no en una pestaña nueva.
- El PDF usa un monograma, no un logo real.
- El código de Órdenes usa terminología hexagonal y debe pasar a Clean Architecture.

### Servicios 2 y 3: faltan funciones

`main` ya implementa login, sesiones, menú por rol, listado de usuarios, alta por invitación y `DesactivarUsuario`.

Falta:

- Generador de nombre de usuario.
- Cambio de contraseña.
- Reporte 1 y Reporte 2, con PDF y Excel. El menú Reportes es hoy un enlace sin destino.
- Verificar que todos los CRUD registren los atributos de auditoría.

### Deuda de arquitectura

- `ClienteService`, `ProductoService`, `VehiculoService` y `MecanicoService` dependen de `MySqlConnector`.
- `MecanicoService`, `ServicioService` y `UsuarioAdministracionService` dependen de Infraestructura.
- `UsuarioAdministracionService` usa `DbConnection` directamente.
- Cuatro vistas inyectan `Infraestructura.Configuracion.ConfiguracionTaller`: `Pages/Index`, `Pages/Historial/Index`, `Pages/Ordenes/Index` y `Pages/Ordenes/_DetalleOrden`.
- `docs/CUMPLIMIENTO_RUBRICA.md` está desactualizado: usa puntajes de otra rúbrica y afirma que ya existen `window.open` y "Refrescar".

## 2. Decisiones

| Servicio | Alcance | Arquitectura | Grupo interno | Integrantes |
|---|---|---|---|---|
| Servicio 1 | Órdenes: transacción principal | Clean Architecture | Grupo 1 | Adrián, Santiago, Gabo |
| Servicio 2 | Usuarios | Hexagonal | Grupo 2 | Ariel, Alex, Alda |
| Servicio 3 | Reportes | Hexagonal | Grupo 2 | Ariel, Alex, Alda |

> **Sobre el puntaje:** según el PDF, cada grupo tiene su propia tabla de 60 puntos (Servicio 1, o Servicios 2 y 3). Los otros 40 (documento 15, Clean Code y SOLID 10, defensa 15) corresponden a ambos grupos.

- La rúbrica no califica el trabajo si todos los servicios usan una única arquitectura. Por eso `Aplicacion/Puertos` y la guía hexagonal **se conservan** para los Servicios 2 y 3.
- `Pages/` se mantiene en su ubicación para no romper las rutas de Razor Pages.
- `Program.cs` es el composition root.
- No se eliminan datos de la base compartida. Cualquier cambio de esquema debe ser idempotente.
- Se prioriza el cambio más pequeño y fácil de defender oralmente.

### Asignación por integrante

Propuesta basada en el historial de commits de cada área; el equipo puede intercambiar tareas.

| Integrante | Grupo | Responsabilidad en código | Parte del documento |
|---|---|---|---|
| Adrián | 1 | Tarea 1: puertos de Órdenes, dominio, casos de uso, `ConfiguracionTaller` y `ArquitecturaTests` | C4 y organización del repositorio |
| Santiago | 1 | Tarea 3 completa y Tarea 1, paso 8 (fugas en servicios de catálogo) | Diagramas de clases y capturas de patrones |
| Gabo | 1 | Tarea 2 completa: transacción, anulación, comprobante y registro de dependencias | Ejemplos SOLID y capturas de Clean |
| Alda | 2 | Tarea 4, bloque A: integrar HU3, usuarios y cambio de contraseña | Capturas de Hexagonal y SBA |
| Alex | 2 | Tarea 4, bloque B: login, bitácora y consultas SQL de reportes | Carátula, introducción, descripción y requisitos |
| Ariel | 2 | Tarea 4, bloque C: presentación de PDF y Excel | Conclusiones y bibliografía |

Todos preparan la defensa y suben el documento al LMS.

---

## 3. Tarea 0 — Confirmaciones e integración previa

**Responsable:** Adrián y Alda coordinan; Alda integra HU3. **Se hace antes de cualquier refactor.**

1. Confirmar la fecha de entrega. El PDF fija la defensa el 03/11/2026, y este plan es del 04/11/2026.
2. Preguntar al docente si acepta servicios lógicos dentro de un solo proyecto. La rúbrica habla de 4 aplicaciones, y `app1`, `app2` y `app3` de `docker-compose.yml` son réplicas de la misma app, no servicios.
   - **Si lo acepta:** un solo `.csproj`, con los servicios separados por carpetas y documentados.
   - **Si no lo acepta:** crear 4 proyectos (Frontend Razor y 3 APIs) con base de datos compartida. Implica sustituir las llamadas directas de `Pages` a fachadas por HTTP y compartir la autenticación, así que este plan debe ampliarse antes de continuar.
3. Fusionar `origin/ALDA/HU3` en `main` **antes** de la Tarea 1, porque agrega puertos y cambia `Program.cs` y `_Layout.cshtml`. Hoy se fusiona sin conflictos, pero 4 de sus pruebas fallan (ver Tarea 4, bloque A).

### Criterios de aceptación

- Fecha y formato SBA confirmados.
- HU3 integrado en `main`, con build y pruebas en verde.

---

## 4. Tarea 1 — Servicio 1: dominio y aplicación en Clean

**Responsable:** Adrián. El paso 8 lo hace Santiago.

**Objetivo:** dejar Órdenes con entidades con comportamiento, contratos y casos de uso explícitos, sin dependencias de MySQL, Razor ni QuestPDF.

**No cambia el comportamiento visible.** Sí toca `Infraestructura`, `Pages`, `Program.cs` y pruebas para actualizar referencias, porque si no el proyecto no compila.

### Pasos

1. Ejecutar la línea base:
   - `dotnet build`
   - `dotnet test tests/TallerMecanico.IntegrationTests/TallerMecanico.IntegrationTests.csproj`
2. Mover **solo** los puertos de Órdenes a `Aplicacion/CasosUso/Ordenes/Contratos/`, con `git mv`:

   | Actual | Nuevo |
   |---|---|
   | `IOrdenPort` | `IOrdenRepository` |
   | `IDetalleOrdenPort` | `IDetalleOrdenRepository` |
   | `IProductoStockOrdenPort` | `IProductoStockRepository` |
   | `ICabeceraOrdenPort` | `ICabeceraOrdenRepository` |
   | `IConsultaOrdenesPort` | `IOrdenQueryRepository` |
   | `IUnidadTrabajoOrdenPort` | `IUnidadTrabajoOrden` |
   | `ITokenOrdenPort` | `ITokenOrden` |
   | `IBorradorOrdenPort` | `IBorradorOrden` |
   | `IComprobanteOrdenPdf` | `IComprobantePdf` |

   Las implementaciones existentes se actualizan en este mismo paso para que la aplicación siga levantando.
3. Enriquecer el dominio:
   - `Orden`: `Anular(usuario, fecha)`, `CalcularTotal()` (repuestos y servicios), `EstaAnulada` y `PuedeAnularse()`.
   - `DetalleOrden`: `CalcularSubtotal()` y validaciones de cantidad y precio.
   - Mantener los setters públicos necesarios para el mapeo ADO y la auditoría que `Orden` ya tiene.
4. Extraer casos de uso en `Aplicacion/CasosUso/Ordenes/`, a partir del código existente:
   - `CrearOrdenCasoUso` y `AnularOrdenCasoUso`, desde `OrdenServicioService`.
   - `ConsultarOrdenesCasoUso`, desde `ConsultaOrdenesService`.
   - `GenerarComprobanteCasoUso`, desde `ComprobanteOrdenService`.
5. Mantener `OrdenServicioFacade` y `ConsultaOrdenesFacade` como fachadas delgadas que delegan en los casos de uso.
6. Reutilizar lo que ya existe: `ICurrentUser` para el usuario y `TimeProvider` para la fecha. No crear `IReloj`.
7. Mover `ConfiguracionTaller` a `Aplicacion/Configuracion`, conservando el Singleton, y actualizar las cuatro vistas que la inyectan.
8. Eliminar las fugas de infraestructura de los servicios de catálogo:
   - Quitar `MySqlConnector` de `ClienteService`, `ProductoService`, `VehiculoService` y `MecanicoService`. Los `catch (MySqlException)` pasan a infraestructura o se transforman en excepciones de aplicación.
   - `MecanicoService` y `ServicioService` dejan de depender de `Creador*`.
9. Patrones a conservar y documentar:
   - Facade: `OrdenServicioFacade`, `ConsultaOrdenesFacade`.
   - Factory Method: `Creador*`, usados solo desde `Program.cs`.
   - Singleton: `ConfiguracionTaller`.
   - Repository y Unit of Work.
10. Pruebas:
    - Revisar `OrdenesTests` y ampliarla solo donde falte: crear, total, stock insuficiente, anular, doble anulación y auditoría, con dobles en memoria.
    - `ArquitecturaTests`: escanear `Dominio`, `Aplicacion` y `Pages`, incluidas las `.cshtml`, y fallar si contienen `TallerMecanico.Infraestructura` o `MySqlConnector`. Esto cubre `using`, `@inject` y nombres completos.
11. Documentación:
    - Nuevo `docs/ARQUITECTURA_CLEAN.md` para el Servicio 1.
    - Actualizar `docs/ARQUITECTURA_HEXAGONAL.md` para que cubra los Servicios 2 y 3.
    - Actualizar `README.md`.
    - Reescribir `docs/CUMPLIMIENTO_RUBRICA.md` con la matriz de la sección 10.

### Criterios de aceptación

- `dotnet build` con 0 warnings y 0 errores.
- Casos de uso probados sin base de datos.
- `ArquitecturaTests` en verde con el alcance anterior.
- `Aplicacion/Puertos` sigue existiendo con los puertos que no son de Órdenes.
- La aplicación sigue levantando y la UI funciona igual.

> `ArquitecturaTests` fallará hasta sacar `DbConnection` y la dependencia de Infraestructura de `UsuarioAdministracionService`. Ese cambio pertenece a la Tarea 4; hay que coordinar el orden entre grupos para que el build quede en verde.

---

## 5. Tarea 2 — Servicio 1: infraestructura, transacción y anulación

**Responsable:** Gabo.

**Objetivo:** adaptar y comprobar el código existente. **No se crean repositorios ni unidades de trabajo nuevos.**

**No toca Razor ni JavaScript.**

### Pasos

1. Confirmar que estas clases implementan los contratos de la Tarea 1 y reutilizan su SQL:
   - `OrdenRepository`, `DetalleOrdenRepository`, `ProductoStockOrdenRepository`, `CabeceraOrdenRepository` y `ConsultaOrdenesRepository`.
   - `MySqlUnidadTrabajoOrden`, con una sola conexión y `ReadCommitted`.
2. Verificar el flujo de creación:
   - Token idempotente.
   - `FOR SHARE` para vehículo, mecánico y servicios; `FOR UPDATE` para productos, ordenados por Id.
   - Precios y totales recalculados desde la base.
   - Inserción de cabecera y detalles, y descuento de stock.
   - Commit o rollback completo.
3. Verificar el flujo de anulación:
   - `SELECT ... FOR UPDATE` de la orden.
   - Si ya está anulada, devolver error sin tocar stock.
   - Estado `Anulada`, con `AnuladoPor` y `FechaAnulacion`.
   - Restitución de stock en la misma transacción.
4. Mantener las columnas `CreadoPor` y `AnuladoPor` para no tocar el esquema compartido. Sin borrados físicos.
5. PDF:
   - `ComprobanteOrdenPdf` implementa `IComprobantePdf` y se mantiene con QuestPDF.
   - Agregar el logo oficial si existe.
6. Registro de dependencias:
   - `Infraestructura/RegistroDependencias.cs` con `AddInfraestructuraOrdenes()`.
   - `Program.cs` solo llama a esa extensión para Órdenes.
7. Revisar `OrdenesMySqlTests` y `ComprobanteMySqlTests`, y completar solo los casos que falten:
   - Stock exacto y rollback tras un fallo posterior al descuento.
   - Token duplicado o concurrente.
   - Stock restituido una sola vez, incluso con dos anulaciones concurrentes.
   - Auditoría correcta y PDF con los datos obligatorios.

### Criterios de aceptación

- Pruebas de integración en verde.
- Commit y rollback demostrables.
- Stock restituido exactamente una vez.
- Sin `DELETE FROM Ordenes`.
- PDF válido y con el contenido obligatorio.

---

## 6. Tarea 3 — Servicio 1: presentación

**Responsable:** Santiago.

**Objetivo:** cerrar lo que falta en la UI sin acoplarla a infraestructura.

### Pasos

1. `Pages/Ordenes/Crear`: conservar buscadores, tabla temporal, creación al vuelo y confirmación.
2. `Pages/Ordenes/Index`:
   - **Agregar el botón Refrescar**, mediante GET a la misma página y conservando los filtros. Basta con refresco manual.
   - Conservar una fila por orden y el modal de solo lectura.
   - Mostrar el creador y los datos de anulación.
3. Comprobante:
   - Tras guardar correctamente, abrir el PDF en una pestaña nueva con `window.open`.
   - Mostrar un enlace visible si el navegador bloquea la pestaña.
   - Mantener el enlace manual Comprobante.
   - Ajustar las pruebas que esperen el modal actual.
4. Pruebas:
   - Pruebas HTTP existentes de crear, listar, detalle y comprobante.
   - Pruebas de navegador de buscadores, líneas temporales, totales, confirmación, creación al vuelo, modal y nueva pestaña.

### Criterios de aceptación

- Los 25 puntos de UI son demostrables.
- El comprobante se abre automáticamente en una pestaña nueva.
- El listado refresca y el modal es de solo lectura.

---

## 7. Tarea 4 — Servicios 2 y 3: usuarios y reportes en Hexagonal

**Responsables:** Alda, Alex y Ariel. Los puertos van en `Aplicacion/Puertos` y los adaptadores en `Infraestructura`.

### Lo que Alda ya dejó en `origin/ALDA/HU3`

No se repite este trabajo; solo se integra y se corrige lo indicado abajo.

- Página de cambio de contraseña con enlace en el menú: pide la actual y dos veces la nueva, y cierra la sesión al terminar.
- Generador de nombre de usuario a partir del correo, con sufijo para evitar duplicados.
- Alta de usuario con activación por correo.
- Reporte de órdenes y reporte por mecánico, con filtros, PDF, Excel y pantalla propia.
- Puertos y adaptadores de reportes, con estructura hexagonal.

Se ejecutaron sus pruebas de reportes, contraseña y nombre de usuario: **41 pasan y 4 fallan**. No se probó con la aplicación en marcha.

### Bloque A — Alda: integración y usuarios

1. Fusionar HU3 en `main` (Tarea 0).
2. Corregir las 4 pruebas de `ClosedXmlReporte*AdapterTests`. Buscan los textos en `sheet1.xml`, y lo más probable es que ClosedXML los guarde en `sharedStrings.xml`; confirmar la causa antes de cambiarlas.
3. Cambiar la contraseña a **mínimo 8 caracteres** en `CambioContrasenaService.cs` y `CambiarContrasena.cshtml`. El login ya acepta claves de 8.
4. Sacar `DbConnection` y `DatabaseConnectionFactory` de `UsuarioAdministracionService`, pasándolos a un adaptador detrás de un puerto. HU3 todavía agrega SQL en ese servicio, y sin este cambio la Hexagonal no es defendible y `ArquitecturaTests` falla.
5. Documentar el protocolo del nombre de usuario.
6. Comprobar en la aplicación: Select sin contraseña, alta con correo de activación y baja con `DesactivarUsuario`.

### Bloque B — Alex: login, bitácora y SQL de reportes

1. Verificar la UI de login y que cada rol vea opciones de menú diferentes.
2. Verificar que cada CRUD registre usuario y fecha en los atributos de auditoría.
3. Corregir en `ConsultaReportesMySqlAdapter`:
   - **Órdenes anuladas contadas como dinero.** El reporte por mecánico suma todas las órdenes en `TotalOrdenes` y `TotalFacturado`, y el de órdenes las incluye en `TotalGeneral` cuando no se filtra por estado. El total facturado debe contar solo las activas.
   - **Servicios incompletos en el Reporte 1.** Solo muestra `Orden.ServicioId`, que es el primer servicio; los demás están en `DetalleServiciosOrdenes`.
4. Agregar pruebas de ambos casos con MySQL aislado.

### Bloque C — Ariel: presentación de PDF y Excel

1. **Corregir el gráfico del PDF** en `QuestPdfReporteMecanicosAdapter.ComponerGrafico`. Cada fila tiene un solo `RelativeItem`, así que, por lectura del código, todas las barras ocuparían el mismo ancho sin importar el monto. Agregar un segundo `RelativeItem` con el espacio restante y comprobarlo en un PDF generado.
2. **Mover los datos sumariados al pie en ambos PDF.** Hoy `ComponerResumen` los pone arriba y la rúbrica los pide en el pie.
3. **Agregar logo al Excel.** Hoy `XlsxBuilder` solo escribe el nombre del taller como texto.
4. **Agregar el gráfico al Excel del Reporte 2,** o confirmar con el docente que basta en el PDF. Hasta donde se sabe, ClosedXML no crea gráficos nativos, así que la opción simple es insertarlo como imagen.
5. Usar el mismo logo que el comprobante del Servicio 1 cuando esté disponible.

### Criterios de aceptación

- Los 60 puntos de la sección 10 son demostrables.
- Los nuevos puertos y adaptadores siguen la estructura hexagonal.
- `ArquitecturaTests` en verde.

---

## 8. Tarea 5 — Documento, repositorio y defensa

**Responsable:** todo el equipo, según la tabla de asignación. Adrián integra el documento final.

### Documento (Anexo 1)

- [ ] Carátula. *(Alex)*
- [ ] Introducción: SOLID, patrones, arquitecturas aplicadas y objetivo. *(Alex)*
- [ ] Descripción: contexto, problemática y requisitos funcionales mínimos. *(Alex)*
- [ ] Diagramas C4 de niveles 1, 2 y 3, y el combinado del 1 al 3. *(Adrián)*
- [ ] Diagramas de clases y capturas de cada patrón de diseño. *(Santiago)*
- [ ] Capturas de Clean *(Gabo)* y de SBA y Hexagonal *(Alda)* en el proyecto.
- [ ] Un ejemplo justificado en el código de cada principio SOLID. *(Gabo)*
- [ ] URL del repositorio y explicación de ramas y commits. *(Adrián)*
- [ ] Conclusiones: aprendizajes sobre SOLID y patrones, dificultades y recomendaciones. *(Ariel)*
- [ ] Bibliografía. *(Ariel)*
- [ ] **Cada integrante sube el documento al LMS.** Quien no lo suba no se evalúa en este punto.

### Defensa y forma

- Preparar a cada integrante en patrones, estilos arquitectónicos, Clean Code, SOLID y el código del proyecto.
- Reemplazar `docs/DEFENSA_T6_HEXAGONAL.md` por una guía que cubra las dos arquitecturas. *(Gabo y Alda)*
- Revisar la ortografía de la UI: Santiago las pantallas del Servicio 1 y Ariel las de los Servicios 2 y 3. Cada error resta 1 punto.

---

## 9. Orden de ejecución

1. Tarea 0: Alda integra HU3 en `main`.
2. Tarea 1: Adrián define los contratos y deja la app funcionando; Santiago limpia los servicios de catálogo.
3. Tareas 2, 3 y 4 en paralelo. Gabo y Alda coordinan los cambios en `Program.cs`, y Santiago y Alda los de `_Layout.cshtml`.
4. Tarea 5, con capturas tomadas sobre el código final.

Cada tarea termina con build y pruebas en verde. No se integra una tarea que rompa la aplicación.

## 10. Matriz de la rúbrica

### General (100)

| Rubro | Pts | Tarea |
|---|---:|---|
| Servicios asignados + frontend integrado | 60 | 1 a 4, según el grupo |
| Documento (Anexo 1) | 15 | 5 |
| Clean Code y SOLID, con enlace al repositorio | 10 | 1, 4 y 5 |
| Defensa y presentación | 15 | 5 |
| Ortografía de la UI (−1 por error) | Penalización | 5 |

### Servicio 1 — Grupo interno 1 (60)

| Rubro | Pts | Tarea |
|---|---:|---|
| Al menos 2 buscadores | 8 | 3 |
| Llamar a otra UI si falta un registro | 5 | 3 |
| Agregar, editar y eliminar registros temporales | 9 | 3 |
| Datos sumariados | 2 | 3 |
| Confirmación al guardar | 1 | 3 |
| Transacción con commit, rollback y auditoría | 10 | 1 y 2 |
| Anulación lógica, restitución y auditoría | 10 | 1 y 2 |
| Select con refresco | 1 | 3 |
| Modal de detalle de solo lectura | 5 | 3 |
| Comprobante con línea gráfica y logo | 1 | 2 |
| Comprobante con datos completos | 7 | 2 |
| Comprobante automático | 1 | 3 |

### Servicios 2 y 3 — Grupo interno 2 (60)

| Rubro | Pts | Tarea |
|---|---:|---|
| Login y manejo de sesiones | 10 | 4 |
| Bitácora en atributos de auditoría | 10 | 4 |
| CRUD de usuario: Select 2, Insert 7, Delete 1 | 10 | 4 |
| Cambio de contraseña | 10 | 4 |
| Reporte 1 | 10 | 4 |
| Reporte 2 | 10 | 4 |

## 11. Verificación final

```powershell
dotnet build
dotnet test tests/TallerMecanico.IntegrationTests/TallerMecanico.IntegrationTests.csproj -- xUnit.ParallelizeTestCollections=false
dotnet msbuild -t:NubeUp
```

Smoke test del Servicio 1:

1. Iniciar sesión.
2. Crear una orden con dos productos y dos servicios.
3. Cambiar cantidades y eliminar una línea.
4. Crear un cliente o vehículo al vuelo y volver sin perder el borrador.
5. Confirmar y guardar.
6. Verificar que el PDF se abre en una pestaña nueva.
7. Refrescar el listado y abrir el modal de detalle.
8. Anular la orden y comprobar que el stock vuelve exactamente a su valor anterior.

Smoke test de los Servicios 2 y 3:

1. Iniciar sesión con cada rol y comparar el menú.
2. Crear un usuario, comprobar el nombre generado y activarlo por correo.
3. Cambiar la contraseña y volver a iniciar sesión con la nueva.
4. Dar de baja al usuario y comprobar la auditoría.
5. Generar ambos reportes con filtros, en PDF y Excel.

## 12. Riesgos y mitigaciones

- **Una sola arquitectura en todos los servicios:** limitar Clean a Órdenes y conservar `Puertos` en los Servicios 2 y 3.
- **Un solo proyecto no impone capas:** usar `ArquitecturaTests` con el alcance de la Tarea 1.
- **El docente no acepta servicios lógicos:** resolverlo en la Tarea 0 antes de refactorizar.
- **Cambios de esquema en Aiven compartido:** usar migraciones idempotentes, sin borrar datos y coordinadas con el equipo.
- **Bloqueo de pestaña nueva:** mostrar un enlace visible.
- **Renombres de namespaces:** usar `git mv` y compilar por tandas.

## 13. Pendientes por confirmar

1. Fecha de entrega y prórroga.
2. Formato SBA aceptado por el docente.
3. Confirmar que Adrián, Santiago y Gabo hacen el Servicio 1, y Ariel, Alex y Alda los Servicios 2 y 3.
4. Logo oficial para PDF y Excel. Si no existe, usar el monograma AP de forma temporal.
5. Si el gráfico del Reporte 2 también debe estar en el Excel.
