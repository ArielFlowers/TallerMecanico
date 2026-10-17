# Buscador compartido

El componente se integra en Mecánicos y Clientes. Los demás CRUD y Nueva Orden siguen pendientes. Los recursos se cargan solamente en la página consumidora; no se modificó `_Layout.cshtml`.

## Archivos y uso posterior

- `Pages/Shared/_BuscadorAutocompletado.cshtml`: etiqueta, combobox, lista, estado y campo ID opcional; no genera un formulario.
- `Pages/Shared/BuscadorAutocompletadoModel.cs`: configuración visual por instancia y límites del cliente.
- `wwwroot/js/autocompletado.js`: comportamiento independiente por elemento `data-autocompletado`.
- `wwwroot/css/autocompletado.css`: estilos encapsulados con las variables de AutoTaller Pro y valores oscuros de respaldo.

La página integradora deberá cargar el CSS y el JavaScript una sola vez e incluir la partial con su modelo. `Id` debe ser único en toda la página; también se generan los sufijos `-texto`, `-id`, `-estado`, `-sugerencias` y `-opcion-N`. No reutilizar esos identificadores en otros elementos.

Configurar `UrlSugerencias` con una URL del mismo origen generada por Razor. Se conservan sus parámetros existentes y se agrega `termino`. `Etiqueta`, `Placeholder`, `NombreCampoTexto`, `NombreCampoId`, `Texto`, `IdSeleccionado` y todos los mensajes se pueden configurar. `MensajeResultados` acepta el marcador `{cantidad}`.

`Texto` e `IdSeleccionado` deben representar el mismo registro cuando se precarga una selección. La inicialización no consulta ni emite una selección. Una etiqueta sin ID se considera texto libre. El componente no hace validaciones de negocio.

Al cargar el archivo se inicializan las instancias automáticamente. Para una partial agregada después, llamar `window.AutoTallerAutocompletado.inicializar(contenedor)`. Puede repetirse sin duplicar listeners.

## Modos y eventos

`ModoFiltrar` (`filtrar`): al elegir se actualiza el campo ID, si fue configurado, y se emite un evento. La página debe decidir cómo filtrar por el ID exacto; el componente no navega ni envía formularios.

`ModoSeleccionar` (`seleccionar`): conserva la sugerencia en el componente y en el campo ID configurado, por ejemplo el futuro `Formulario.MecanicoId`. No agrega productos ni escribe datos automáticamente. Si se modifica el texto, se borra el ID anterior.

Los dos eventos se emiten desde la raíz y propagan hacia sus ancestros (`bubbles: true`):

- `autocompletado:seleccion`: `detail` contiene `idInstancia`, `modo` y `sugerencia` (`id`, `textoPrincipal`, `textoSecundario`).
- `autocompletado:invalidacion`: `detail` contiene `idInstancia`, `modo` e `idAnterior`. Se emite cuando editar invalida una selección existente, no en cada pulsación sin selección.

La página puede escuchar el evento de su instancia o delegarlo en un contenedor. No debe interpretar nombres de funciones desde atributos ni ejecutar texto como código. La selección usa una copia de los datos, sin exponer el estado interno mutable.

Flechas arriba/abajo recorren las opciones; Enter acepta la activa e impide el submit accidental; Escape cierra y cancela sin seleccionar; Tab mantiene el comportamiento normal de foco. Enter sin una opción activa conserva el comportamiento del formulario de la página. `reset()` restaura los valores iniciales del formulario sin emitir una nueva selección.

## Contrato de los futuros endpoints

Petición: GET a `UrlSugerencias` con `termino`, credenciales del mismo origen, `Accept: application/json` y `X-Requested-With: XMLHttpRequest`.

Respuesta 200, `Content-Type: application/json` (puede incluir charset), con un arreglo directo:

```json
[
  { "id": 12, "textoPrincipal": "Juan Pérez", "textoSecundario": "CI: 1234567 - Motores" },
  { "id": 15, "textoPrincipal": "María Muñoz" }
]
```

Los ID deben ser números enteros positivos y únicos. `textoPrincipal` es obligatorio y no puede estar vacío. `textoSecundario` puede omitirse o ser null; el componente lo convierte en cadena vacía. No se aceptan entidades envueltas en otro objeto ni HTML para representar opciones. Los textos se insertan con `textContent`.

Un arreglo vacío indica ausencia de coincidencias. HTTP 401/403 o una redirección presentan el mensaje de sesión/permisos; errores HTTP, HTML en lugar de JSON, JSON mal formado, datos incompatibles y fallos de red presentan el mensaje general. No se muestran detalles técnicos del servidor.

El cliente espera 300 ms, busca desde dos caracteres y representa como máximo diez opciones. **El endpoint debe validar el término y limitar a diez en SQL**: recortar en el navegador no es una protección del servidor. También debe comprobar roles y devolver solo los datos necesarios.

Cada instancia cancela su temporizador y fetch anterior y comprueba una versión de petición antes de actualizar la interfaz. Cerrar, editar o resetear impide que respuestas pendientes reabran la lista. Cancelar fetch no garantiza por sí solo cancelar una consulta SQL futura.

## Accesibilidad y diseño

Se sigue el patrón combobox de selección manual: foco en el input, lista y opciones con roles, `aria-expanded`, `aria-controls`, `aria-activedescendant`, selección activa y estado anunciado. Referencia: https://www.w3.org/WAI/ARIA/apg/patterns/combobox/.

CSS sin selectores globales, lista desplazable, opciones de texto largo, foco visible y reducción de movimiento. Contenedores padres con overflow restrictivo pueden recortar el desplegable: habrá que verificarlo al integrar modales reales. Aún falta una revisión con lector de pantalla, además de las comprobaciones automáticas de ARIA y teclado.

## Pruebas sin base de datos

`AutocompletadoTests` crea un host HTTP únicamente dentro de xUnit, renderiza la partial real con dos configuraciones y ejecuta `tests/autocompletado_ui.cjs` en Edge. No ejecuta el `Program.cs` de producción, no registra persistencia y no usa MySQL. Las peticiones de sugerencias se simulan en el navegador; algunas ignoran deliberadamente abort para comprobar las respuestas fuera de orden.

Configurar `PLAYWRIGHT_MODULE` con una instalación existente de Playwright, como en las pruebas de navegador del proyecto, y ejecutar:

```powershell
dotnet build
dotnet test tests/TallerMecanico.IntegrationTests/TallerMecanico.IntegrationTests.csproj --filter 'FullyQualifiedName~Autocompletado'
```

Sin `PLAYWRIGHT_MODULE`, el caso de navegador se omite explícitamente; el test de configuración se ejecuta. No hacen falta paquetes nuevos ni TALLER_TEST_MYSQL. Se puede usar `TALLER_AUTOCOMPLETADO_CAPTURA` para guardar una captura móvil en una carpeta temporal.

## Mecánicos - Iteración 2

`Pages/Mecanicos/Index` configura el modo `filtrar` y el endpoint GET `?handler=Sugerencias&termino=...`. Conserva la política `SoloAdministrador` de la carpeta, también para este handler. La respuesta no se almacena en caché y contiene únicamente el arreglo de sugerencias.

`MecanicoService.SugerirAsync` normaliza espacios y rechaza términos de menos de 2 o más de 100 caracteres con un arreglo vacío, sin consultar MySQL. El puerto específico `IConsultaMecanicosPort` lo implementa el repositorio existente, creado por `CreadorMecanico`. Se mantiene la composición actual del servicio (y su dependencia concreta anterior para el CRUD), sin añadir operaciones al repositorio genérico ni otra jerarquía de fábricas.

El SQL usa parámetros y `LIMIT 10` parametrizado. CI se compara por prefijo; cada palabra del término debe coincidir al inicio de alguna palabra de Nombres, PrimerApellido o SegundoApellido. Se escapan `!`, `%` y `_`. Las cláusulas dinámicas contienen exclusivamente índices generados internamente, nunca valores del usuario. La colación de consulta `utf8mb4_0900_ai_ci` ignora tildes y diferencias de caja; requiere MySQL 8 y columnas utf8mb4, como el esquema actual. No altera las colaciones ni restricciones del esquema. Se propaga la cancelación HTTP hasta ADO.NET.

La proyección incluye nombre sin espacios sobrantes y CI con complemento cuando exista, más especialidad. No expone auditoría, teléfono ni entidades completas. SegundoApellido vacío o NULL es tolerado.

Seleccionar emite el evento compartido y envía el formulario GET con `MecanicoSeleccionadoId`, separado de `MecanicoId` usado para escrituras. El listado consulta ese ID exacto; un ID inexistente muestra cero registros. Editar el texto invalida el ID automáticamente. Sin ID, Enter/Buscar mantienen **sin cambios** la búsqueda manual parcial anterior, incluyendo especialidad, celular, género y complemento. Limpiar navega al listado sin filtros. Formularios POST y redirecciones conservan ambos filtros; si se elimina el registro seleccionado, queda un listado vacío hasta limpiar.

### Verificación de Mecánicos

`MecanicosAutocompletadoTests` comprueba términos cortos sin conexión, prefijos, palabras compuestas, tildes, apellidos vacíos/NULL, límite aplicado por el repositorio, comodines, inyección, ID exacto y regresión de búsqueda manual. Compara los registros antes y después de consultar para detectar escrituras accidentales.

`MecanicosHttpTests` ejecuta la aplicación real por HTTPS contra una base aleatoria creada por `MySqlAisladoFixture`. Comprueba JSON, permisos (anónimo y Recepcionista), formularios y filtros tras crear/editar/eliminar. Con `PLAYWRIGHT_MODULE` también ejecuta `tests/mecanicos_autocompletado_ui.cjs` en Edge contra el endpoint real: selección con ratón/teclado, edición, invalidación, búsqueda manual, limpiar y viewport móvil. Reutiliza la cookie del administrador de pruebas para no consumir intentos adicionales de login. Sin Playwright se ejecutan las comprobaciones HTTP, pero no ese bloque de navegador.

Configurar `TALLER_TEST_MYSQL` exclusivamente con un servidor destinado a pruebas; el fixture crea y elimina su propia base `autotaller_test_<GUID>`, nunca utiliza `taller_mecanico`. `PLAYWRIGHT_MODULE` señala el módulo Playwright ya instalado. Opcionalmente `TALLER_MECANICOS_CAPTURA` guarda una captura móvil fuera del repositorio.

```powershell
dotnet build
dotnet test tests/TallerMecanico.IntegrationTests/TallerMecanico.IntegrationTests.csproj --filter 'FullyQualifiedName~Mecanicos|FullyQualifiedName~Autocompletado'
```

Sin `TALLER_TEST_MYSQL`, los casos que necesitan MySQL se omiten explícitamente. La comprobación móvil automatizada no sustituye una revisión con lector de pantalla ni una prueba en un teléfono físico.

## Clientes

`Pages/Clientes/Index` configura la misma partial en modo `filtrar`, con el placeholder `Buscar por CI, nombres o apellidos...`. Carga los mismos recursos compartidos sin cambiar el componente ni Mecánicos. El pequeño listener de selección permanece junto al JavaScript ya existente de la página. No se agregan estilos propios de autocompletado.

El endpoint GET `?handler=Sugerencias&termino=...` conserva la política `PersonalAutorizado` (Administrador y Recepcionista), devuelve un arreglo directo `id/textoPrincipal/textoSecundario` y usa `Cache-Control: no-store`. `ClienteService.SugerirAsync` rechaza términos normalizados de menos de 2 o más de 100 caracteres sin consultar el puerto. La espera de 300 ms sigue siendo responsabilidad del componente.

La nueva operación de lectura de `IClientePort` la implementa el `ClienteRepository` existente, que continúa siendo producto de `CreadorCliente`; no cambian las fábricas, el repositorio genérico ni DI. La consulta asíncrona parametrizada aplica `LIMIT 10` en MySQL y propaga la cancelación HTTP. Busca prefijos de CI base o CI-complemento y comienzos de palabra en nombres/apellidos, incluyendo combinaciones de palabras. Usa la misma colación `utf8mb4_0900_ai_ci` y escape de comodines que Mecánicos. Solo proyecta los datos necesarios para construir las sugerencias, sin auditoría ni teléfonos.

El texto secundario muestra el CI con su complemento para distinguir registros con el mismo número base y/o nombres idénticos. La selección usa siempre `ClienteSeleccionadoId`, separado de `ClienteId` para escrituras. Sin selección se conserva intacta la consulta `Search` anterior, también por celular, complemento, fragmentos y nombre concatenado. Editar el texto elimina el ID seleccionado con el comportamiento compartido.

Buscar, seleccionar, limpiar, refrescar y los formularios mantienen `BorradorVehiculoId` y `OrdenBorradorId`. Los retornos especiales después de crear un cliente continúan priorizando el borrador de Vehículos y después el de Orden, igual que antes. Los borradores mantienen sus comprobaciones de sesión/usuario y vencimiento. La selección de clientes dentro de Vehículos no se modifica. Tampoco se cambian las validaciones: en Clientes el segundo apellido sigue siendo obligatorio al registrar/editar; el autocompletado tolera registros anteriores con apellido vacío sin generar espacios sobrantes.

### Pruebas de Clientes

`ClientesAutocompletadoTests` verifica consultas limitadas reales, prefijos, nombres compuestos, tildes, complementos distintos, escape e inyección, ausencia de escrituras, semántica manual y retornos a ambos borradores. `ClientesHttpTests` ejecuta la aplicación real por HTTPS contra MySQL aislado y comprueba permisos, contrato JSON, límite 10, ID exacto, creación, edición, errores y conservación de filtros/contextos. Con `PLAYWRIGHT_MODULE`, ejecuta `tests/clientes_autocompletado_ui.cjs`: teclado, ratón, invalidación, limpiar, edición, contextos, IDs HTML únicos y viewport móvil. Opcionalmente `TALLER_CLIENTES_CAPTURA` guarda la captura fuera del repositorio. Reutiliza las cookies de prueba, sin cambiar el límite de login real.

Con `TALLER_TEST_MYSQL` apuntando **únicamente a un servidor aislado de pruebas**, ejecutar:

```powershell
dotnet build
dotnet test tests/TallerMecanico.IntegrationTests/TallerMecanico.IntegrationTests.csproj --filter 'FullyQualifiedName~Clientes|FullyQualifiedName~Autocompletado|FullyQualifiedName~VehiculosTests'
```

Si el ejecutable Debug está abierto, se puede verificar sin interrumpirlo usando `--configuration VerificacionClientes` en ambos comandos. El host HTTPS de Clientes utiliza el ensamblado compilado de la configuración que se está probando, no una aplicación local existente. No se cambia el esquema de producción ni se accede a `taller_mecanico`.

## Integración pendiente

Productos y los demás módulos requieren autorización antes de integrarse. Nueva Orden necesitará su propio endpoint de lectura con sus permisos, sin ampliar el acceso al CRUD de Mecánicos. Las reglas de stock, transacciones e idempotencia siguen a cargo de los casos de uso existentes.
