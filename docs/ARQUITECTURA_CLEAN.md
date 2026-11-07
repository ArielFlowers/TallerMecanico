# Clean Architecture: Servicio 1 (Órdenes y catálogos)

AutoTaller Pro se organiza como un producto de cuatro aplicaciones. El Frontend
solo presenta la interfaz y consume las APIs por HTTP; cada servicio tiene su
propio proyecto, su composición y su ciclo de vida. El Servicio 1 aplica Clean
Architecture y concentra las órdenes y los catálogos que participan en la
transacción.

| Aplicación | Proyecto | Alcance | Arquitectura | Guía |
|---|---|---|---|---|
| Frontend | `frontend-app/TallerMecanico.Frontend.csproj` | Razor Pages, sesión, navegación y clientes HTTP | Presentación | Este documento |
| Servicio 1 | `backend-services/servicio-ordenes` | Órdenes, inventario, catálogos y dashboard | Clean Architecture | Este documento |
| Servicio 2 | `backend-services/servicio-usuarios` | Usuarios, autenticación y auditoría | Hexagonal | [ARQUITECTURA_HEXAGONAL.md](ARQUITECTURA_HEXAGONAL.md) |
| Servicio 3 | `backend-services/servicio-reportes` | Reportes PDF/Excel | Hexagonal | [ARQUITECTURA_HEXAGONAL.md](ARQUITECTURA_HEXAGONAL.md) |

Los contratos compartidos viven en `shared/TallerMecanico.Contratos`. No contienen
lógica de negocio ni referencias a MySQL, Razor o librerías de documentos.

## Comunicación entre aplicaciones

```text
Navegador ──► frontend-app (Razor Pages)
                    │  HTTP/JSON + X-Clave-Interna + X-Usuario-*
                    ├──► servicio-ordenes   (Clean)
                    ├──► servicio-usuarios  (Hexagonal)
                    └──► servicio-reportes  (Hexagonal)
                                │
                                └──► MySQL compartida
```

- El Frontend agrega la clave interna y las cabeceras del usuario autenticado en
  `frontend-app/Api/CabecerasInternasHandler.cs`.
- Cada API valida la clave con `ClaveInternaHandler` y repite el control de roles
  con `[Authorize]`.
- `ApiBase` traduce `400` a `ValidationException`, `403` a
  `UnauthorizedAccessException` y `503` a `HttpRequestException`, de modo que las
  páginas muestran los mismos errores que antes.
- Solo `servicio-ordenes` crea y actualiza el esquema de la base compartida.
- El token idempotente de la orden permanece en la sesión del Frontend. La API
  recibe el token en el DTO y lo usa para detectar reenvíos.

## Capas del Servicio 1

| Capa | Ubicación | Responsabilidad |
|---|---|---|
| Dominio | `Dominio/Entidades/` | Reglas de `Orden`, `DetalleOrden`, catálogos e historial. No depende de nadie |
| Casos de uso | `Aplicacion/CasosUso/Ordenes/` | Una clase por acción del negocio |
| Contratos | `Aplicacion/CasosUso/Ordenes/Contratos/` | Lo que los casos de uso necesitan de afuera |
| Fachadas | `Aplicacion/Fachadas/` | Punto de entrada para los controladores |
| Servicios de catálogo | `Aplicacion/Servicios/` | Clientes, vehículos, mecánicos, productos, servicios y dashboard |
| Infraestructura | `Infraestructura/` | MySQL, QuestPDF y seguridad; implementa los contratos |
| Presentación HTTP | `Controllers/` | Controladores MVC que reciben los DTO compartidos |
| Composición | `Program.cs` e `Infraestructura/RegistroDependencias.cs` | Conecta cada contrato con su implementación |

```text
Controllers  ──►  Fachadas  ──►  Casos de uso  ──►  Dominio
                                     ▲
Infraestructura ── implementa ── Contratos
```

Las dependencias apuntan hacia el núcleo. Como cada servicio es un proyecto
separado, la regla se comprueba con
`tests/servicio-ordenes.Tests/ArquitecturaTests.cs`: falla si `Dominio`,
`Aplicacion` o `Controllers` mencionan `TallerMecanico.Infraestructura`,
`MySqlConnector`, `QuestPDF` o `ClosedXML`. La lista de deuda pendiente está vacía.

## Módulos del Servicio 1

| Módulo | Tablas principales | Componentes |
|---|---|---|
| Órdenes | `Ordenes`, `DetalleOrdenes`, `DetalleServiciosOrdenes` | `OrdenesController`, fachadas, `CrearOrdenCasoUso`, `AnularOrdenCasoUso`, `ConsultarOrdenesCasoUso`, `GenerarComprobanteCasoUso` |
| Inventario | `Productos` | `ProductosController`, `ProductoService`, `ProductoStockOrdenRepository` |
| Catálogo | `Clientes`, `Vehiculos`, `Mecanicos`, `Servicios`, `HistorialCostosServicios` | `ClientesController`, `VehiculosController`, `MecanicosController`, `ServiciosController`, servicios y validaciones |
| Dashboard | Consultas de conteo | `DashboardController` y `DashboardService` |

Una creación de orden toca los tres primeros módulos dentro de la misma
transacción: inserta la venta, sus detalles y descuenta el stock de inventario. Por
eso viven en el mismo servicio y no se separan en transacciones distribuidas.

## Casos de uso

| Clase | Qué hace |
|---|---|
| `CrearOrdenCasoUso` | Registra la orden, sus detalles y el descuento de stock |
| `AnularOrdenCasoUso` | Anula la orden y restituye el stock |
| `ConsultarOrdenesCasoUso` | Lista, sugiere y obtiene el detalle |
| `GenerarComprobanteCasoUso` | Genera el comprobante PDF |

## Reglas del dominio

| Regla | Dónde |
|---|---|
| Una orden anulada no se puede volver a anular | `Orden.PuedeAnularse()` y `Orden.Anular()` |
| La anulación registra usuario y fecha | `Orden.Anular()` |
| El total suma repuestos y servicios | `Orden.CalcularTotal()` |
| Subtotal, cantidad y precio válidos | `DetalleOrden.CalcularSubtotal()` y `DetalleOrden.EsValido()` |

Los setters siguen públicos porque los usa el mapeo ADO al leer de MySQL. Es una
concesión a propósito para mantener el código simple.

## Flujo: crear una orden

1. `Pages/Ordenes/Crear` valida el token de la sesión y llama a
   `OrdenesApi.CrearAsync`.
2. `POST api/ordenes` entra a `OrdenesController`, que convierte el DTO y llama a
   `OrdenServicioFacade.CrearOrden`.
3. `CrearOrdenCasoUso` valida la solicitud y abre la transacción con
   `IUnidadTrabajoOrden`.
4. Comprueba si el token ya se usó, valida vehículo, mecánico y servicios, y
   bloquea los productos.
5. Valida el stock con los precios de la base y calcula subtotales y total con el
   dominio.
6. Inserta la cabecera, los servicios y los repuestos, y descuenta el stock.
7. Actualiza el total y confirma. Si algo falla antes de confirmar, la transacción
   se revierte.

## Flujo: anular una orden

1. `Pages/Ordenes/Index` llama a `OrdenesApi.AnularAsync`.
2. `POST api/ordenes/{id}/anular` entra a `OrdenesController` y llama a
   `OrdenServicioFacade.AnularOrden`.
3. `AnularOrdenCasoUso` comprueba que el usuario sea Administrador y bloquea la
   orden.
4. Si `orden.PuedeAnularse()` es falso, responde con error sin tocar el stock.
5. Bloquea los productos y aplica `orden.Anular(usuario, fecha)`.
6. Guarda el estado y la auditoría, y restituye el stock.
7. Confirma, o revierte todo si algo falla.

## Patrones de diseño

| Patrón | Dónde | Para qué |
|---|---|---|
| Facade | `OrdenServicioFacade`, `ConsultaOrdenesFacade` | Los controladores no conocen los casos de uso |
| Factory Method | `Infraestructura/Persistencia/Fabricas/Creador*` | Centraliza la creación de repositorios |
| Singleton | `Aplicacion/Configuracion/ConfiguracionTaller` | Una sola configuración del taller |
| Repository | `IOrdenRepository` y `OrdenRepository` | Separa el SQL de los casos de uso |
| Unit of Work | `IUnidadTrabajoOrden` y `MySqlUnidadTrabajoOrden` | Una transacción para todos los repositorios |
| Adapter | MySQL, QuestPDF y autenticación por clave interna | Conecta los contratos con tecnologías concretas |

## Principios SOLID

| Principio | Ejemplo | Justificación |
|---|---|---|
| Responsabilidad única | `CrearOrdenCasoUso` y `AnularOrdenCasoUso` | Cada clase cambia por un solo motivo |
| Abierto/cerrado | `IComprobantePdf` | Otro formato de comprobante sería otra implementación |
| Sustitución de Liskov | `IUnidadTrabajoOrden` | Las pruebas usan unidades propias y el caso de uso funciona igual |
| Segregación de interfaces | `IOrdenRepository` e `IOrdenQueryRepository` | Quien solo consulta no depende de escrituras |
| Inversión de dependencias | `CrearOrdenCasoUso` recibe `IUnidadTrabajoOrden` | El caso de uso depende del contrato, no de MySQL |

## Estado de la migración

La separación en cuatro aplicaciones se integra por partes. En esta rama:

- El Servicio 1 ya expone órdenes y catálogos por HTTP.
- El Frontend ya consume órdenes, catálogos y dashboard mediante `Api/`.
- Los Servicios 2 y 3 tienen sus proyectos, contratos y clientes HTTP preparados;
  su implementación se integra desde las ramas de usuarios y reportes.
- El Frontend ya no tiene la copia de órdenes y catálogos. Solo conserva, de forma
  temporal, el código de usuarios y reportes, que se retira al integrar las ramas
  de los Servicios 2 y 3.

La documentación de este archivo se concentra en el Servicio 1. Los detalles de
usuarios y reportes están en `docs/ARQUITECTURA_HEXAGONAL.md`.

## Diagramas

Los diagramas del entregable se mantienen como código PlantUML en
`docs/diagramas/`:

- `Estilo_Arquitectonico_Combinado.puml`
- `C4_Nivel1_Contexto.puml`, `C4_Nivel2_Contenedores.puml`,
  `C4_Nivel3_Componentes.puml` y `C4_Vista_Combinada.puml`
- `Clases_Dominio_Ordenes.puml`, `Clases_CasosUso_Contratos.puml` y
  `Clases_Persistencia_Transaccion.puml`
- `Secuencia_CrearOrden.puml` y `Secuencia_AnularOrden.puml`

## Pruebas

```powershell
dotnet test TallerMecanico.sln
dotnet test tests/servicio-ordenes.Tests/servicio-ordenes.Tests.csproj
dotnet test tests/frontend-app.Tests/frontend-app.Tests.csproj
```

- `OrdenDominioTests`: reglas del dominio, sin base de datos.
- `OrdenesTests`, `OrdenesApiTests`: casos de uso y API HTTP del Servicio 1.
- `OrdenesMySqlTests`, `ConsultaOrdenesMySqlTests`, `ComprobanteMySqlTests`:
  transacciones y consultas sobre MySQL aislado. Requieren `TALLER_TEST_MYSQL`;
  se omiten si no está configurado.
- `ArquitecturaTests`: regla de dependencias del Servicio 1.
- `frontend-app.Tests`: clientes HTTP, DTOs, fachadas y páginas del Frontend.

- `servicio-ordenes.Tests` también tiene las pruebas web de catálogos y órdenes:
  levantan el Frontend y el Servicio 1 como procesos y preparan los datos con los
  servicios de este proyecto.
- `OrdenesPaginasTests`: páginas de órdenes con el Servicio 1 simulado (token,
  servicio no disponible, fecha, borrador y comprobante).

Con `TALLER_TEST_MYSQL` configurado, la solución completa dio 806 pruebas aprobadas,
0 fallidas y 9 omitidas. Las omitidas son de navegador o de extracción de texto PDF,
que requieren herramientas opcionales.
