# Servicios: incorporación pendiente y validación final

## Estado del catálogo real

No se consultó ni modificó `taller_mecanico` durante esta iteración. Por tanto,
"No verificado" **no significa que el dato no exista**. Las descripciones y tiempos
ficticios de las pruebas aisladas no deben incorporarse a la base habitual.

| Servicio | Precio inicial (Bs) | Descripción existente o faltante | Tiempo existente o faltante | Coincidencias por confirmar | Acción necesaria |
|---|---:|---|---|---|---|
| Cambio de aceite | 250 | No verificado | No verificado | Registro exacto, similares y duplicados | Auditar, elegir Id inequívoco o confirmar datos nuevos; cantidad fija 1 |
| Cambio de filtros | 100 | No verificado | No verificado | Registro exacto, similares y duplicados | Auditar y confirmar descripción/tiempo; unidad |
| Cambio de pastillas de freno | 250 | No verificado | No verificado | Registro exacto, similares y duplicados | Auditar y confirmar descripción/tiempo; unidad |
| Diagnóstico con escáner | 120 | No verificado | No verificado | Registro exacto, similares y duplicados | Auditar y confirmar descripción/tiempo; cantidad fija 1 |
| Reparación de motor | 1500 | No verificado | No verificado | Registro exacto, similares y duplicados | Auditar y confirmar descripción/tiempo; cantidad fija 1 |
| Cambio de bujías | 180 | No verificado | No verificado | Registro exacto, similares y duplicados | Auditar y confirmar descripción/tiempo; unidad |
| Reparación de suspensión | 400 | No verificado | No verificado | Registro exacto, similares y duplicados | Auditar y confirmar descripción/tiempo; unidad |
| Cambio de batería | 550 | No verificado | No verificado | Registro exacto, similares y duplicados | Auditar y confirmar descripción/tiempo; cantidad fija 1 |
| Cambio de embrague | 1000 | No verificado | No verificado | Registro exacto, similares y duplicados | Auditar y confirmar descripción/tiempo; cantidad fija 1 |
| Alineación y balanceo | 180 | No verificado | No verificado | Registro exacto, similares y duplicados | Auditar y confirmar descripción/tiempo; cantidad fija 1 |

Con el destino y la consulta autorizados, utilizar `InicializadorCatalogoServicios.Analizar()`
después de preparar el esquema 008. Reutilizar las descripciones y tiempos existentes
válidos. Resolver las ambigüedades por Id y confirmar los datos obligatorios que falten
antes de `Incorporar(...)`, siguiendo [el procedimiento existente](catalogo-servicios.md).
No hay incorporación automática ni fallback a servicios fuera del catálogo.

## Importes mostrados

Nueva Orden muestra el costo vigente de MySQL, cantidad y subtotal. Los valores
visuales son estimaciones; la validación y el cálculo definitivo siguen en el servidor.
`ServicioOrdenDetalle.PrecioUnitario` se deriva del subtotal histórico dividido por
la cantidad; una cantidad antigua no positiva utiliza divisor 1 para evitar división
por cero, sin modificar el importe guardado. Modal y PDF muestran `Costo` como
subtotal y nunca lo vuelven a multiplicar. Se conserva la apertura automática
actual del comprobante en el modal de la página.

## Ocho fallos preexistentes documentados

No se corrigen ni se cambian sus aserciones en esta iteración:

| Prueba | Diferencia previamente comprobada |
|---|---|
| `OrdenesTests.Validacion_RechazaSinServicio` | Espera `ServicioId`, pero el error usa `ServicioIds` |
| `OrdenesMySqlTests.CrearOrden_FacadeGuardaDosProductosTotalYUsuario` | Espera 46,60 sin incluir el servicio; el resultado es 71,60 |
| `OrdenesMySqlTests.ProductosRepetidos_SeAgrupanAntesDelStockYDeLosDetalles` | Misma expectativa anterior de total sin servicio |
| `OrdenesMySqlTests.PreciosManipuladosEnJson_NoCambianPreciosNiTotalDeLaBase` | La solicitud de prueba omite el servicio obligatorio |
| `OrdenesMySqlTests.MigracionOrdenes_SePuedeRepetirSinEliminarDatos` | Intenta eliminar Ordenes mientras existe la FK de DetalleServiciosOrdenes |
| `NuevaOrdenAutocompletadoTests.Borradores_RestauranIdsTextosYDetalleAlSalirHaciaLosTresCrud` | Espera texto del selector legado en lugar de la selección múltiple |
| `NuevaOrdenAutocompletadoTests.Guardar_ValidaIdsStockPreciosDelServidorYConservaIdempotencia` | La preparación sin ServicioIds detiene la validación antes de las aserciones posteriores |
| `NuevaOrdenAutocompletadoTests.Consultas_ReutilizanPrefijosPalabrasTildesLimitesSqlYSoloLectura` | Espera sugerencias vacías para una consulta vacía donde la interfaz ofrece el listado |

Para el cierre previo al merge quedan la regresión ampliada de órdenes, permisos,
inventario y otros consumidores compartidos, y la validación del catálogo real
con los metadatos autorizados. Las pruebas focalizadas no sustituyen esa regresión.
