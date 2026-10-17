# Buscador compartido

El componente se integra en Mecánicos, Clientes, Productos y Vehículos. Los demás CRUD y Nueva Orden siguen pendientes. Los recursos se cargan solamente en la página consumidora; no se modificó `_Layout.cshtml`.

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

## Productos

`Pages/Productos/Index` configura el mismo componente en modo `filtrar`, con el placeholder `Buscar por código o nombre...`. Reutiliza los recursos compartidos y conserva el handler GET `Buscar` para la búsqueda manual. No se cambia la tabla, los modales ni sus acciones; solo se agregan los campos necesarios para conservar filtro y contexto. La partial antigua `_BuscadorProducto` queda sin cambios, aunque esta página ya no la consume.

El endpoint GET `?handler=Sugerencias&termino=...` pasa por `ProductoService` e `IProductoPort` hasta el `ProductoRepository` existente, creado por `CreadorProducto`. No se modifican las fábricas ni los registros de DI. El servicio normaliza únicamente los espacios del término y devuelve `[]` sin consultar para términos de menos de 2 o más de 100 caracteres. La espera de 300 ms y cancelación/descarte de respuestas antiguas siguen siendo del componente. La cancelación HTTP también se propaga a ADO.NET.

El SQL selecciona solo Id, Nombre y Codigo. Busca por prefijo del código o por inicio de palabra del nombre, combinando las palabras del término con AND. Usa parámetros para todos los valores, escapa `!`, `%` y `_`, limita con `LIMIT @Limite` (10) y ordena por Nombre, Codigo e Id. La colación de consulta `utf8mb4_0900_ai_ci` ignora caja y tildes sobre las columnas utf8mb4 del esquema actual; requiere MySQL 8 y no altera el esquema. Solo devuelve `id`, `textoPrincipal` (nombre) y `textoSecundario` (código), sin precio, stock ni auditoría.

La selección envía un GET con `ProductoSeleccionadoId`, separado de `ProductoId` para escrituras, y filtra por el ID exacto. Editar el texto invalida el ID. Sin selección se mantiene intacto el `Search` anterior, con coincidencias parciales por código/nombre. Limpiar recupera todo el listado. Un ID inexistente muestra cero resultados. Los filtros y `OrdenBorradorId` se conservan en búsquedas, formularios, errores, redirecciones, limpiar y refrescar. El retorno especial tras crear desde Orden continúa siendo el existente.

### Permisos e inventario

Se conserva `PersonalAutorizado` para lectura (Administrador y Recepcionista), incluyendo sugerencias. Los POST de actualizar/eliminar siguen exigiendo Administrador. Crear conserva su excepción previa: Recepcionista solo con un borrador de orden válido en su sesión. No se amplía ningún permiso, ni se agrega un endpoint para Nueva Orden.

Las sugerencias son exclusivamente SELECT: no descuentan/restituyen stock, calculan precios ni ejecutan transacciones de órdenes. Como pertenecen al listado administrativo existente, también pueden mostrar productos sin stock; las reglas de selección para una orden se resolverán en su futura integración, sin reutilizar permisos de escritura de inventario.

### Pruebas de Productos

`ProductosAutocompletadoTests` verifica límites de entrada, prefijos, nombres compuestos, caja/tildes, comodines y comillas literales, inyección, límite SQL, ID exacto, búsqueda manual, validaciones/CRUD, retorno a borrador y permisos de Recepcionista. Compara los registros completos (incluyendo precio, stock y auditoría) antes y después de las consultas.

`ProductosHttpTests` utiliza el ejecutable real por HTTPS con MySQL aislado y prueba JSON, límite 10, anónimos, ambos roles de lectura, denegación de escrituras de Recepcionista, CRUD del Administrador y conservación de filtros/contexto. Con `PLAYWRIGHT_MODULE`, ejecuta `tests/productos_autocompletado_ui.cjs`: ratón, teclado, invalidación, selección de registros con nombres iguales, edición, buscar, limpiar, stock cero, IDs HTML únicos y viewport móvil. `TALLER_PRODUCTOS_CAPTURA` permite guardar una captura temporal. No usa la base local `taller_mecanico`.

Con `TALLER_TEST_MYSQL` apuntando exclusivamente al servidor aislado de pruebas y Playwright configurado:

```powershell
dotnet build
dotnet test tests/TallerMecanico.IntegrationTests/TallerMecanico.IntegrationTests.csproj --filter 'FullyQualifiedName~TallerMecanico.IntegrationTests.Productos|FullyQualifiedName~TallerMecanico.IntegrationTests.AutocompletadoTests'
```

Sin MySQL se omiten los casos correspondientes; sin Playwright se ejecutan las verificaciones HTTP, pero no el bloque de navegador. La prueba móvil automatizada no sustituye una prueba en teléfono físico o con lector de pantalla.

## Vehículos

`Pages/Vehiculos/Index` utiliza la misma partial en modo `filtrar`, con el placeholder `Buscar por placa, marca o modelo...`. Carga los recursos compartidos sin modificarlos. Se retiró `Pages/Shared/_BuscadorVehiculo.cshtml` después de comprobar que esta página era su único consumidor de ejecución (la ubicación real era Shared, no Vehiculos). El listener específico solo envía el formulario GET al seleccionar, sin duplicar el componente.

El endpoint GET `?handler=Sugerencias&termino=...` utiliza `VehiculoService`, `IVehiculoPort` y el repositorio existente creado por `CreadorVehiculo`. Normaliza espacios del término, acepta entre 2 y 100 caracteres y devuelve `[]` sin consultar fuera de esos límites. No cambia las validaciones ni la normalización de placas al registrar/editar, incluidas las extranjeras. No se agregan criterios por cliente u otros datos.

La consulta asíncrona proyecta solamente Id, Placa, Marca y Modelo. Busca prefijo de placa o inicio de palabra en marca/modelo, combinando palabras entre ambos campos. Usa parámetros, escape literal de `!`, `%` y `_`, `LIMIT @Limite` (10) y orden `Placa, Id`. La colación de consulta `utf8mb4_0900_ai_ci` ignora caja/tildes, con los mismos requisitos de MySQL 8 y columnas utf8mb4 ya documentados; no modifica el esquema. La cancelación HTTP llega hasta ADO.NET. La respuesta contiene únicamente `id`, `textoPrincipal` (placa), `textoSecundario` (marca y modelo); no consulta ni expone información de clientes, auditoría o kilometraje.

Seleccionar usa `VehiculoSeleccionadoId`, distinto de `Formulario.Id`/`id` para escrituras. El listado recupera ese ID exacto; editar el texto invalida la selección. Sin ID se conserva íntegro el `Search` manual anterior por fragmentos de placa, marca o modelo y su orden existente. Limpiar vuelve al listado completo. El formulario GET envía explícitamente `handler=Buscar`, porque un submit GET puede reemplazar los parámetros query del action; así filtrar no se confunde con el ingreso inicial desde Nueva Orden que abre el modal de creación.

### Relaciones, borradores y permisos

Se mantienen `OrdenBorradorId` y `ClientePreseleccionadoId` al buscar, limpiar, refrescar y enviar formularios. Los retornos especiales después de guardar continúan apuntando a Nueva Orden. `BorradorVehiculo` agrega únicamente `VehiculoSeleccionadoId` nullable; el parámetro final opcional de `IBorradorVehiculoPort.Guardar` permite guardar/restaurar ese filtro al registrar un cliente y retomar el vehículo. Las llamadas anteriores y los borradores JSON sin el nuevo atributo siguen siendo válidos. No hay migración de base de datos.

`_CamposVehiculo.cshtml` solo incorpora un hidden para conservar el filtro. Los selectores de cliente, marca/modelo, el toggle extranjero y sus validaciones no se refactorizan. La asociación real sigue siendo `Formulario.ClienteId` y se mantiene su comprobación en el servicio. El buscador/selector de Nueva Orden permanece intacto. Las sugerencias son solo SELECT, sin stock ni transacciones de órdenes.

Se conserva `PersonalAutorizado` de la carpeta Vehículos (Administrador y Recepcionista), incluyendo el nuevo handler. No se amplían ni restringen las operaciones previas de esos roles. Se utiliza `Cache-Control: no-store`, y los anónimos no pueden obtener sugerencias.

### Pruebas de Vehículos

`VehiculosAutocompletadoTests` prueba límites, espacios, placas nacionales/extranjeras, prefijos, marca/modelo compuestos, tildes, escape/inyección, límite real del repositorio, búsquedas sin escrituras y retorno a Clientes/Nueva Orden con el filtro exacto. `VehiculosAutocompletadoHttpTests` prueba el contrato y los roles sobre la aplicación real en HTTPS/MySQL aislado, además de CRUD, relaciones y filtros. Con Playwright ejecuta `tests/vehiculos_autocompletado_ui.cjs`: selección con teclado/ratón, invalidación, búsqueda manual, limpiar sin abrir otro modal, retorno real por Clientes conservando el formulario y viewport móvil. `TALLER_VEHICULOS_CAPTURA` permite guardar una captura temporal.

También se ejecutan las pruebas anteriores de Vehículos y de los módulos ya integrados. Configurar `TALLER_TEST_MYSQL` exclusivamente para un servidor aislado y `PLAYWRIGHT_MODULE` con una instalación existente:

```powershell
dotnet build
dotnet test tests/TallerMecanico.IntegrationTests/TallerMecanico.IntegrationTests.csproj --filter 'FullyQualifiedName~TallerMecanico.IntegrationTests.Vehiculos|FullyQualifiedName~TallerMecanico.IntegrationTests.AutocompletadoTests'
```

Para la regresión completa de buscadores, añadir al filtro las clases de Mecánicos, Clientes y Productos. Sin MySQL se omiten los casos correspondientes; sin Playwright se omite la verificación de navegador. Las pruebas móviles usan un viewport, no un teléfono físico. No se accede a la base local `taller_mecanico`.

## Integración pendiente

Servicios, el selector de clientes dentro de Vehículos y los selectores de Nueva Orden requieren autorización antes de integrarse. Nueva Orden necesitará sus propios endpoints de lectura con sus permisos, sin ampliar el acceso al CRUD de Mecánicos ni los permisos de escritura del inventario. Las reglas de stock, transacciones e idempotencia siguen a cargo de los casos de uso existentes.
