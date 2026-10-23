# H2 — Cierre de verificación e integración

Fecha: 23 de octubre de 2026. Cierre definitivo: **LISTA PARA PUSH Y PULL REQUEST**.
Las pruebas específicas y reales de H2 pasan; no se declara verde la regresión
completa. Los nueve fallos observados se reprodujeron con idéntico mensaje en main
intacto y en main + H2. El equipo aceptó expresamente el riesgo de appsettings.json.
No se modificó producción durante esta iteración ni se corrigieron otros módulos.

## Resultados, sin sumar ejecuciones solapadas

| Ejecución | Aprobadas | Fallidas | Omitidas |
| --- | ---: | ---: | ---: |
| Redis/configuración/sesiones | 113 | 0 | 0 |
| Regresión segura inicial | 363 | 0 | 107 |
| PDF real y componente en navegador, anfitrión | 5 | 0 | 0 |
| MySQL aislado, primer intento paralelo | 456 | 10 | 4 |
| MySQL aislado, rama, sin paralelismo | 461 | 5 | 4 |
| Copia integrada main + H2 + pendientes, sin MySQL | 362 | 1 | 107 |
| Copia integrada main + H2 + pendientes, MySQL aislado | 457 | 9 | 4 |
| Cuatro fallos adicionales sobre origin/main intacto | 0 | 4 | 0 |

El build de la rama y el de la copia integrada terminaron con 0 errores y
0 advertencias. Los TRX están
ignorados en `tests/h2/TestResults/`; los nombres, motivos y resultados se conservan
en `resultado-iteracion6.json`. Las 113 específicas ya forman parte de la regresión.
Los cinco casos de PDF/navegador incluyen dos que ya estaban aprobados: no son cinco
pruebas nuevas de cobertura.

## Las 107 omisiones

- 103 requerían `TALLER_TEST_MYSQL` para bases generadas `autotaller_test_*`.
- Una requería MySQL y Playwright/Edge (VehiculosNavegadorTests).
- Una requería solamente Playwright/Edge (AutocompletadoTests.PartialReal).
- Dos requerían Python con pypdf para inspeccionar el PDF real de QuestPDF.
- Ninguna de estas 107 requería credenciales personales ni correo real.

Se habilitaron MySQL en un SDK temporal sobre la red interna H2, y PDF/componente
en el anfitrión. Balance de las 107: **101 aprobadas, cuatro aserciones fallidas y
dos escenarios sin cobertura funcional por necesitar navegador y MySQL juntos**.
Uno de estos últimos no se marca como omitido en el TRX: NuevaOrdenComprobanteHttpTests
falla en su precondición por ausencia de PLAYWRIGHT_MODULE/Edge en el SDK Linux.
El otro es VehiculosNavegadorTests. No se debilitó ninguna aserción.

Cobertura combinada de los mismos 470 casos: 464 aprobados, cuatro fallos de
aserción y dos casos pendientes de infraestructura. **No es una ejecución 470/470**.
Se excluyeron aparte diez casos de seis clases que usan User Secrets/conexión local
o correo real: RepositoriosMySqlTests, ConfirmacionEmailMySqlTests,
LimiteReenvioMySqlTests, RenovacionTokenMySqlTests, RevisionSolicitudesMySqlTests y
EnvioGmailRealTests. No están incluidos en las 107 ni se ejecutaron.

## Fallos reproducibles y alcance

En la rama, sin paralelismo:

1. `OrdenesMySqlTests.CrearOrden_FacadeGuardaDosProductosTotalYUsuario` y
   `ProductosRepetidos_SeAgrupanAntesDelStockYDeLosDetalles`: esperan 46,60; el
   resultado es 71,60 porque incluye el servicio de 25. El primer caso también
   compara el total con solo los subtotales de productos.
2. `OrdenesMySqlTests.PreciosManipuladosEnJson_NoCambianPreciosNiTotalDeLaBase`:
   su JSON omite el servicio obligatorio y espera éxito.
3. `ConsultaOrdenesHttpTests.ListadoHttps_AutorizaLosRolesYConservaFiltrosAlRefrescar`:
   espera «Refrescar»; la vista aprobada contiene «Limpiar filtros».
4. `NuevaOrdenComprobanteHttpTests.Navegador_PostTradicionalFechaAutomaticaComprobanteErroresBloqueoYRecarga`:
   falta la dependencia de navegador; no acredita fallo funcional del comprobante.

Los timeouts de Clientes/Vehículos/Servicios/Productos y el 429 de Mecánicos del
primer intento no se repitieron al desactivar paralelismo. No se cambió el límite
de login, el timeout de los clientes ni las aserciones para obtener ese resultado.

La integración añade cuatro fallos, reproducidos también en origin/main SIN H2:

- `OrdenesTests.Validacion_RechazaSinServicio`: espera ServicioId; main usa ServicioIds.
- `NuevaOrdenAutocompletadoTests.Borradores_RestauranIdsTextosYDetalleAlSalirHaciaLosTresCrud`:
  espera TextoServicio/ServicioId del formulario de servicio único; main normaliza a ServicioIds.
- `NuevaOrdenAutocompletadoTests.Guardar_ValidaIdsStockPreciosDelServidorYConservaIdempotencia`:
  el fixture antiguo no satisface ServicioIds y se detiene antes de la validación esperada.
- `OrdenesMySqlTests.MigracionOrdenes_SePuedeRepetirSinEliminarDatos`: el fixture
  intenta borrar Ordenes sin considerar la nueva FK de DetalleServiciosOrdenes.
  Es el montaje de la prueba, no una demostración de fallo de la migración productiva.

Trabajo separado de H2: revisar con el responsable de órdenes estos contratos y
fixtures, conservando el propósito de las aserciones. La comparación definitiva
inferior demuestra que estos fallos ya ocurren en main y no bloquean específicamente
Redis según el criterio aprobado. No se modificaron órdenes ni sus pruebas.
No se afirma cobertura completa de Nueva Orden en main.

## Comparación definitiva con main intacto

Main remoto sigue en `95a6a6f`; no cambió desde la Iteración 6. Se verificaron sus
407 archivos contra los objetos Git: cero diferencias de contenido, considerando
solo conversión CRLF/LF de la exportación Windows, sin modificar ningún archivo.
Los siete archivos de producción de I5 coinciden con la copia H2 ya probada.

Se ejecutó exactamente el mismo filtro de nueve pruebas en main y main + H2:
misma imagen SDK `sha256:747cbade5bfd918a175ac32f8f1d931edec2c7d0d5397a3de72eaef1ac1f66d2`,
mismo script, sin paralelismo, mismo MySQL aislado y fixtures que crean bases
autotaller_test_* con datos equivalentes. Los nombres aleatorios de bases/Ids no
se reutilizaron. Sin correo, Playwright/Edge ni Python en ambos SDK. Fuente y
paquetes de solo lectura; no se utilizó la contraseña de appsettings.

Resultado de la selección: **main 0/9/0; main + H2 0/9/0**
(aprobadas/fallidas/omitidas). Los nueve mensajes de error coinciden exactamente,
normalizando únicamente saltos de línea. No son dos regresiones completas nuevas.
El build volvió a terminar con 0 errores/advertencias; las 113 específicas pasaron.

H2 en la tabla significa «rama H2 / copia integrada». P = pasa; F = falla.
Todos los nombres pertenecen a `TallerMecanico.IntegrationTests`.

| Prueba | Main | H2 | Causa | Clasificación | Acción necesaria |
| --- | --- | --- | --- | --- | --- |
| OrdenesMySqlTests.CrearOrden_FacadeGuardaDosProductosTotalYUsuario | F | F / F | Espera 46,60; recibe 71,60 al incluir servicio de 25. | Preexistente | Revisar total esperado y comparación de subtotales en otro trabajo. |
| OrdenesMySqlTests.ProductosRepetidos_SeAgrupanAntesDelStockYDeLosDetalles | F | F / F | Mismo total sin costo del servicio. | Preexistente | Adaptar la expectativa económica sin quitar la comprobación de agrupación. |
| OrdenesMySqlTests.PreciosManipuladosEnJson_NoCambianPreciosNiTotalDeLaBase | F | F / F | JSON sin servicio obligatorio; espera éxito. | Preexistente | Completar fixture válido antes de probar manipulación de precios. |
| ConsultaOrdenesHttpTests.ListadoHttps_AutorizaLosRolesYConservaFiltrosAlRefrescar | F | F / F | Espera Refrescar; vista contiene Limpiar filtros. | Preexistente | Revisar expectativa del texto, fuera de H2. |
| NuevaOrdenComprobanteHttpTests.Navegador_PostTradicionalFechaAutomaticaComprobanteErroresBloqueoYRecarga | F | F / F | No hay PLAYWRIGHT_MODULE/Edge; falla antes del flujo funcional. | Precondición de infraestructura preexistente | Ejecutar posteriormente con navegador + MySQL; no atribuir un defecto al PDF. |
| OrdenesTests.Validacion_RechazaSinServicio | F | P / F | Espera ServicioId; contrato de main devuelve ServicioIds. | Preexistente en main | Adecuar prueba al contrato multiservicio en otro trabajo. |
| NuevaOrdenAutocompletadoTests.Borradores_RestauranIdsTextosYDetalleAlSalirHaciaLosTresCrud | F | P / F | Espera texto/propiedad de servicio único; main usa ServicioIds. | Preexistente en main | Verificar borrador multiservicio en la prueba correspondiente. |
| NuevaOrdenAutocompletadoTests.Guardar_ValidaIdsStockPreciosDelServidorYConservaIdempotencia | F | P / F | Fixture no satisface ServicioIds y no alcanza el error esperado de vehículo. | Preexistente en main | Completar fixture multiservicio sin debilitar aserciones. |
| OrdenesMySqlTests.MigracionOrdenes_SePuedeRepetirSinEliminarDatos | F | P / F | Preparación intenta borrar Ordenes sin considerar FK_DetalleServiciosOrdenes_Ordenes. | Preexistente en main | Corregir montaje aislado de la prueba, no el esquema productivo. |

TRX ignorados: `TestResults/cierre-main/integracion-mysql.trx` y
`TestResults/cierre-integrada/integracion-mysql.trx`. Mensajes y procedencia
conservados en `resultado-iteracion6.json`, sección `comparacionCierre`.
**Cero fallos nuevos atribuibles a H2 entre los observados.** El fallo de navegador
es una precondición demostrada en ambos; el flujo funcional sigue sin evaluarse
en esos SDK. No hay ningún fallo observado que quede sin clasificar.

## Pruebas reales H2

- Redis PONG en A/B/C; Nginx distribuyó solicitudes a las tres réplicas.
- MISS en A: cada COUNT pasó de 23 a 24. HIT en B: los tres permanecieron en 24.
  El HIT todavía verifica al usuario en MySQL; no significa cero SQL de todo tipo.
- `autotaller:dashboard:resumen`: PTTL 57791 ms tras MISS y 54423 ms tras HIT;
  expiración natural a -2. Generación compartida sin expiración (TTL -1).
- Tres entradas inválidas reparadas; cinco carreras sincronizadas correctas,
  incluyendo rechazo de publicación obsoleta y pérdida/recreación de generación.
- Seis altas/bajas reales de Mecánicos/Vehículos/Servicios invalidaron. Diez
  ediciones/errores/no-op no invalidaron. Antiforgery ausente: 400; Recepcionista:
  redirección a acceso denegado; Administrador: invalidación confirmada.
- La primera ejecución de invalidación se detuvo por margen de TTL insuficiente;
  se preparó únicamente la clave de resumen H2 y la repetición completa pasó.
- Cookie y antiforgery cruzados; token y borradores de orden/vehículo compartidos
  entre réplicas y por Nginx. TTL de sesión cercano a 1800 s, deslizante.
- Redis caído: dashboard 200/NO DISPONIBLE con MySQL; lectura y escritura de
  borradores 503, sin falsa confirmación ni detalles técnicos expuestos.
- Redis recuperado: PONG A/B/C, nueva generación, MISS +3 COUNT, HIT +0, nuevos
  borradores disponibles. Los anteriores se perdieron: Redis es volátil.
- Expiración de sesiones/sobres comprobada con TTL y vencimiento forzado de datos
  propios; no se esperaron 30 minutos naturales.
- Login en A, Nueva Orden en B: 200; logout sin antiforgery: 400; logout válido en
  B: 302; acceso protegido posterior desde C: 302 a Login.

Pasaron pruebas MySQL/HTTP de CRUD, búsquedas, permisos, administración de usuarios,
revocación, inventario, rollback, anulaciones y comprobante MySQL. Los fallos de
órdenes y los dos escenarios de navegador pendientes impiden afirmar «todo pasa».

## Seguridad y configuración

**Riesgo aceptado expresamente por el equipo, no bloqueante:** `appsettings.json:3`, clave
`ConnectionStrings:MySqlConnection`, contiene contraseña literal no vacía.
También está en main. `DatabaseConnectionFactory` lee GetConnectionString y
`MySqlConnectionFactory` crea la conexión MySQL. No hay indicios claros de
placeholder; no se ha confirmado que funcione. El usuario comunicó que el equipo
autorizó mantenerla visible para este proyecto. No se mostró, copió en informes o
logs, utilizó para autenticar ni modificó. No se alteraron configuración, contraseña
ni historial y este punto no bloquea el merge de H2.

Los archivos H2 añadidos no revelaron credenciales reales en la revisión realizada:
las coincidencias revisadas son lecturas de variables, ejemplos o errores simulados.
`local.env` y TestResults están ignorados. Las pruebas solo usaron credenciales
aleatorias H2; no se montaron User Secrets ni se envió correo.

Host/puerto/TTL y fallback se validan; diagnósticos GET/SET/invalidation diferencian
HIT/MISS/error/deshabilitado y no imprimen excepciones con secretos. El endpoint
de diagnóstico permanece restringido a Development. Los auxiliares se excluyen
de la compilación/publicación de producción; no se añadieron paquetes.

Riesgos conocidos, no nuevos bloqueos funcionales de H2:

- `allkeys-lru` puede expulsar sesiones además del resumen; sin persistencia,
  reiniciar Redis pierde borradores. Para durabilidad futura: separar sesiones,
  revisar política de memoria/persistencia o almacenamiento persistente. No se
  autorizaron ni aplicaron esos cambios; H2 no promete conservarlos tras reinicio.
- No hay transacción distribuida MySQL/Redis. Invalidación fallida puede dejar
  datos anteriores; el TTL no equivale a consistencia estricta desde el commit.
- Confianza del proxy: Program.cs confía en 172.19.0.0/16; H2 tiene 172.18.0.0/16.
  Las pruebas H2 HTTP/Development NO acreditan forwarded HTTPS en despliegue real.
  Verificar la red efectiva de Nginx antes de desplegar, sin ampliar confianza a todos.

## Git, despliegue y archivos pendientes

- Rama: `refactor-h2-entregable2`; HEAD `199f71a`.
- main remoto confirmado: `95a6a6f` (dos commits nuevos; varios servicios por orden).
- Diferencia: dos commits solo main y cinco solo rama. H2 I1–I4 están en commits
  6293f60, 9d8c0ea, 061d1be y 199f71a; I5 sigue pendiente de commit.
  El quinto commit de la rama es el cambio anterior del celular, no I5.
- Sin conflictos textuales en merge-tree. Se comprobó en copia temporal ignorada
  la integración con los quince archivos pendientes de I5, sin tocar ramas/index.
- Reconstruir imagen y actualizar coordinadamente A/B/C. No mezclar escritores
  antiguos SET/DEL con el protocolo nuevo de generación. Conservar la misma
  instancia Redis y Data Protection compartida; sin nuevos secretos/paquetes.
- Seis servicios H2 activos; contenedor ajeno sin cambios. Los fixtures eliminaron
  sus bases autotaller_test_*; al cierre solo queda la base H2 y las de sistema.
  Se conservan clientes fixture/historial de precios propios para auditoría.

Pendientes de I5, preservados:

- `Infraestructura/Configuracion/ConfiguracionRedis.cs`
- `Infraestructura/Sesion/BorradorOrdenSesion.cs`
- `Infraestructura/Sesion/BorradorVehiculoSesion.cs`
- `Infraestructura/Sesion/TokenOrdenSesion.cs`
- `Infraestructura/Sesion/ConfirmarSesionFilter.cs`
- `Infraestructura/Sesion/SesionDisponible.cs`
- `Program.cs`
- `tests/TallerMecanico.IntegrationTests/ConfiguracionRedisTests.cs`
- `tests/TallerMecanico.IntegrationTests/SesionDisponibleTests.cs`
- `tests/h2/README.md`
- `tests/h2/INFORME-ITERACION5.md`
- `tests/h2/SesionRedisProbe/Program.cs`
- `tests/h2/SesionRedisProbe/SesionRedisProbe.csproj`
- `tests/h2/resultado-iteracion5.json`
- `tests/h2/sesiones_real.cjs`

Añadidos en I6: `tests/h2/autenticacion_real.cjs`,
`tests/h2/ejecutar_regresion_mysql.sh`, este informe y `resultado-iteracion6.json`.
README enlaza el cierre. Los resultados de ejecución/copias temporales no se
agregaron al seguimiento de Git. No hubo commit, push, merge, cambio de rama,
FLUSHALL/FLUSHDB, eliminación de volúmenes ni operaciones sobre MySQL habitual.

## Decisión

**SÍ: LISTA PARA EL COMMIT FINAL, PUSH Y PULL REQUEST A MAIN.** Se aplica el caso A
aprobado por el usuario: los nueve fallos de integración también ocurren en main
intacto con idéntica causa. Registrar los defectos de pruebas/fixtures de órdenes
para otro trabajo; no se requiere corregirlos en H2. El riesgo de appsettings está
aceptado por el equipo. La falta de dos pruebas de navegador+MySQL sigue siendo
un límite declarado de cobertura, no un defecto nuevo de Redis.

La regresión completa NO está verde; el PR debe declarar sus fallos preexistentes.
No se modificó código productivo ni expectativas de pruebas en el cierre. Los
19 archivos pendientes listados arriba deben incluirse en el commit final;
local.env y TestResults no se incluyen. Mensaje propuesto:
`fix(redis): cerrar H2 con sesiones distribuidas y regresion verificada`.
No se hizo commit, push ni merge automáticamente.
