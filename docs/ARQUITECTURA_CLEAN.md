# Clean Architecture: Servicio 1 (Órdenes)

El proyecto combina dos arquitecturas internas, como pide la rúbrica del 3er Entregable:

| Servicio | Alcance | Arquitectura | Guía |
|---|---|---|---|
| Servicio 1 | Órdenes: transacción principal | Clean Architecture | Este documento |
| Servicios 2 y 3 | Usuarios y Reportes | Hexagonal | [ARQUITECTURA_HEXAGONAL.md](ARQUITECTURA_HEXAGONAL.md) |

Los servicios están separados de forma lógica dentro de un solo proyecto.

## Capas

| Capa | Ubicación | Responsabilidad |
|---|---|---|
| Dominio | `Dominio/Entidades/` | Reglas de `Orden` y `DetalleOrden`. No depende de nadie |
| Casos de uso | `Aplicacion/CasosUso/Ordenes/` | Una clase por acción del negocio |
| Contratos | `Aplicacion/CasosUso/Ordenes/Contratos/` | Lo que los casos de uso necesitan de afuera |
| Fachadas | `Aplicacion/Fachadas/` | Punto de entrada para las páginas |
| Infraestructura | `Infraestructura/` | MySQL, QuestPDF y sesión; implementa los contratos |
| Presentación | `Pages/Ordenes/` | Pantallas Razor. No se mueve porque rompería sus rutas |
| Composición | `Program.cs` e `Infraestructura/RegistroDependencias.cs` | Conecta cada contrato con su implementación |

## Regla de dependencias

```text
Pages  ──►  Fachadas  ──►  Casos de uso  ──►  Dominio
                                 ▲
Infraestructura ── implementa ── Contratos
```

Las dependencias apuntan hacia el núcleo. Como todo está en un solo proyecto, el compilador no lo impide, y por eso `tests/frontend-app.Tests/ArquitecturaTests.cs` falla si `Dominio`, `Aplicacion` o `Pages` mencionan `TallerMecanico.Infraestructura` o `MySqlConnector`. Los archivos con deuda pendiente están en una lista dentro de esa prueba.

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
| Subtotal, y cantidad y precio válidos | `DetalleOrden.CalcularSubtotal()` y `DetalleOrden.EsValido()` |

Los setters siguen públicos porque los usa el mapeo ADO al leer de MySQL. Es una concesión a propósito para mantener el código simple.

## Flujo: crear una orden

1. `Pages/Ordenes/Crear` llama a `OrdenServicioFacade.CrearOrden`, que comprueba el permiso y el token del formulario.
2. `CrearOrdenCasoUso` valida la solicitud y abre la transacción con `IUnidadTrabajoOrden`.
3. Comprueba si el token ya se usó, valida vehículo, mecánico y servicios, y bloquea los productos.
4. Valida el stock con los precios de la base, y calcula subtotales y total con el dominio.
5. Inserta la cabecera, los servicios y los repuestos, y descuenta el stock.
6. Actualiza el total y confirma. Si algo falla antes de confirmar, la transacción se revierte.

## Flujo: anular una orden

1. `Pages/Ordenes/Index` llama a `OrdenServicioFacade.AnularOrden`.
2. `AnularOrdenCasoUso` comprueba que el usuario sea Administrador y bloquea la orden.
3. Si `orden.PuedeAnularse()` es falso, responde con error sin tocar el stock.
4. Bloquea los productos y aplica `orden.Anular(usuario, fecha)`.
5. Guarda el estado y la auditoría, y restituye el stock.
6. Confirma, o revierte todo si algo falla.

## Patrones de diseño

| Patrón | Dónde | Para qué |
|---|---|---|
| Facade | `OrdenServicioFacade`, `ConsultaOrdenesFacade` | Las páginas no conocen los casos de uso |
| Factory Method | `Infraestructura/Persistencia/Fabricas/Creador*` | Centraliza la creación de repositorios |
| Singleton | `Aplicacion/Configuracion/ConfiguracionTaller` | Una sola configuración del taller |
| Repository | `IOrdenRepository` y `OrdenRepository` | Separa el SQL de los casos de uso |
| Unit of Work | `IUnidadTrabajoOrden` y `MySqlUnidadTrabajoOrden` | Una transacción para todos los repositorios |

## Principios SOLID

| Principio | Ejemplo | Justificación |
|---|---|---|
| Responsabilidad única | `CrearOrdenCasoUso` y `AnularOrdenCasoUso` | Antes un servicio hacía ambas cosas; ahora cada clase cambia por un solo motivo |
| Abierto/cerrado | `IComprobantePdf` | Otro formato de comprobante sería otra implementación, sin tocar `GenerarComprobanteCasoUso` |
| Sustitución de Liskov | `IUnidadTrabajoOrden` | Las pruebas usan unidades de trabajo propias en lugar de `MySqlUnidadTrabajoOrden`, y el caso de uso funciona igual |
| Segregación de interfaces | `IOrdenRepository` e `IOrdenQueryRepository` | Quien solo consulta no depende de las operaciones de escritura |
| Inversión de dependencias | `CrearOrdenCasoUso` recibe `IUnidadTrabajoOrden` | El caso de uso depende del contrato, y MySQL queda como un detalle reemplazable |

## Límites conocidos

- **Separación lógica:** es un solo `.csproj`, así que las capas se protegen con `ArquitecturaTests`, que busca texto en los archivos. No es un análisis del compilador.
- **Piezas compartidas:** los casos de uso usan `ICurrentUser`, que sigue en `Aplicacion/Puertos`, y `ValidacionOrdenes`, en `Aplicacion/Validaciones`. También usan `ILogger` de .NET.

## Pruebas

```powershell
dotnet test tests/servicio-ordenes.Tests/servicio-ordenes.Tests.csproj
```

- `OrdenDominioTests`: reglas del dominio, sin base de datos.
- `OrdenesTests`: casos de uso con dobles en memoria.
- `OrdenesMySqlTests`: transacción real sobre el MySQL aislado. Requiere `TALLER_TEST_MYSQL`; ver el README.
- `ArquitecturaTests`: regla de dependencias de `backend-services/servicio-ordenes`.

El código de órdenes y catálogos se copió a `backend-services/servicio-ordenes`. Mientras las páginas
del Frontend no lo consuman por HTTP, el Frontend conserva su copia y sus pruebas de páginas siguen en
`tests/frontend-app.Tests`. Cuando ya no la use, se borra.
