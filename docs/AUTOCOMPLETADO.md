# Buscador compartido - Iteración 1

El componente todavía no está integrado en ningún CRUD ni en Nueva Orden. No hay endpoints nuevos de producción, consultas SQL ni cambios en permisos. No se modificó `_Layout.cshtml`.

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

El cliente espera 300 ms, busca desde dos caracteres y representa como máximo diez opciones. **El endpoint deberá validar el término y limitar a diez en SQL**: recortar en el navegador no es una protección del servidor. También deberá comprobar roles y devolver solo los datos necesarios. No se implementa ninguno de esos endpoints en esta iteración.

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

## Integración pendiente

Mecánicos será el primer consumidor, solo con autorización: configurar URL y nombres de campos; agregar su endpoint de lectura, servicio/puerto/SQL limitado y autorización; escuchar selección e invalidación para filtrar por ID; conservar la búsqueda manual y los filtros al recargar. Después se integrarán los otros módulos. La validación final de IDs y las reglas de stock, transacciones e idempotencia seguirán a cargo de los casos de uso existentes.
