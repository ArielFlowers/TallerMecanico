> **Registro fechado anterior a la retirada del modo local.** Los resultados y comandos históricos se conservan como evidencia. El flujo vigente usa solo Aiven; consultar [README.md](../README.md) y [VALIDACION_NUBE_UNICA.md](VALIDACION_NUBE_UNICA.md).

# Validación de la migración a MySQL 8.4

> Resultados históricos del 27-28/10/2026. La seed exportada y la capa de build
> descritas aquí fueron sustituidas el 29/10/2026; se conservan las fechas y los
> límites de estas comprobaciones. Ver [validación actual](VALIDACION_SIMPLIFICACION.md).

La implementación parte de `c0bebbc` en `feature/entorno-remoto-compartido` y se
realiza en `feature/bd-nube`. Las comprobaciones comenzaron el 27 de octubre de
2026. Este informe distingue los resultados ejecutados de las comprobaciones
que requieren intervención humana o una segunda PC.

## Configuración y conservación de datos

- MySQL 8.4 en los cuatro Compose que incluyen base de datos y en los clientes
  de los scripts de nube. No hay referencias operativas a `mysql:8.0`.
- Los siete scripts siguen la sección 12 del plan recibido, con imagen
  `mysql:8.4` y nombres de dump que incluyen segundos y milisegundos para evitar
  reemplazar respaldos del mismo minuto. Tienen sintaxis válida y contenido ASCII
  para PowerShell 5.1.
- El proyecto local `tallermecanico-local-84` y su volumen nuevo permiten
  conservar el contenedor original `taller-mecanico-mysql` y el volumen
  `tallermecanico_taller-mysql-data`. El original permanece detenido.
- Se exportó `respaldos/dump_local_20261027_2328.sql`: 15 450 bytes, 11 tablas,
  2 usuarios y 3 registros de auditoría. Las tablas de negocio no tenían filas.
  El dump no contiene triggers, `DEFINER` ni `GTID_PURGED`.
- La restauración local en MySQL 8.4.13 conservó todos los conteos. El arranque
  creó `DetalleServiciosOrdenes`, completando las 12 tablas, y recreó el trigger.
- Se confirmó la importación a Aiven antes de ejecutar el importador. Se guardó
  un respaldo previo del destino vacío y se compararon los conteos importados
  con el origen: coinciden.
- Aiven ejecuta MySQL 8.4.11 y permite 76 conexiones. La serie es 8.4 en todos
  los entornos; el proveedor administra el nivel de parche de su servicio.

## Comprobaciones ejecutadas

- Configuración Compose válida para compartido con build, local, anfitrión,
  pruebas generales y H2. El Compose de anfitrión no se inició.
- Construcción Docker de la aplicación correcta.
- MySQL local, MySQL de pruebas y MySQL H2 iniciaron correctamente en 8.4.
  En H2 se verificaron las opciones de triggers y general log habilitadas.
- Stack local completo: login HTTP 200, tres identificadores de réplica por
  Nginx y Redis `PONG`.
- Stack conectado a Aiven: primera réplica inicializada, 12 tablas y trigger
  presentes; después, login HTTP 200, tres identificadores de réplica y Redis
  `PONG` con el stack completo. El MySQL original permaneció detenido.
- Aiven real: creación y ejecución del trigger auxiliar correctas, con una fila
  de historial. La base auxiliar se eliminó al finalizar la prueba.
- Se creó `respaldos/dump_nube_20261027_2358.sql` tras migrar, con las 12 tablas.
- El seed inicial se generó sin usuarios, solicitudes, auditoría ni `CREATE TABLE`.
  Tenía cero INSERT porque no había datos de negocio. Se aplicó dos veces al MySQL
  local 8.4 con conteos idénticos. Posteriormente se regeneró con datos de negocio
  y se validó completo como se detalla en la prueba del seed poblado al final de
  este informe. Sigue pendiente la revisión humana antes de incorporarlo a Git.
- Para comprobar el seed solo se inició el MySQL local, sin interrumpir el stack
  web conectado a Aiven. Al terminar se detuvo ese MySQL y se conservó su volumen.
- El importador rechaza la ejecución sin `-Confirmar` antes de conectar a Aiven.
- `.env`, `bd-nube.env`, respaldos y certificados quedaron fuera de una
  exportación real del contexto Docker. El seed también se excluye del build.
- Las credenciales, certificados, dumps y resultados de pruebas están ignorados
  por Git. No se modificó código C#, Razor, Dockerfile ni Nginx.

## Regresión automatizada y comparación con MySQL 8.0

La selección segura existente ejecutó **505 pruebas** contra un MySQL 8.4
temporal local: **487 aprobadas, 13 fallidas y 5 omitidas**. Se excluyeron las
seis clases que pueden usar conexiones antiguas de User Secrets o correo real.
No se ejecutó `dotnet test` contra Aiven ni contra una base de trabajo.

Los mismos 13 casos se ejecutaron con el mismo código compilado contra un
MySQL 8.0 temporal independiente. Diez reprodujeron inmediatamente el mismo
mensaje; tres HTTP tuvieron inicialmente timeouts y, al repetir únicamente esos
casos sin la carga de los otros contenedores de pruebas, reprodujeron también
los mismos mensajes. **Los 13 fallos son anteriores al cambio de versión.**

| Grupo | Casos fallidos | Causa observada en ambas versiones |
|---|---:|---|
| Órdenes MySQL | 4 | Totales sin costo del servicio, fixture de servicio inválido y eliminación de una tabla con una FK todavía existente. |
| Nueva Orden, pruebas de aplicación | 3 | Expectativas de servicio único y de búsqueda vacía que no corresponden al comportamiento actual. |
| Nueva Orden HTTP | 1 | Se espera una lista vacía y la aplicación devuelve vehículos. |
| Consulta de órdenes HTTP | 1 | Se espera el texto `Refrescar`, ausente en la vista actual. |
| Productos HTTP | 1 | La respuesta actual incluye la propiedad adicional `stock`. |
| Validación de órdenes | 1 | Se espera `ServicioId`; el contrato actual devuelve `ServicioIds`. |
| Navegador de órdenes y servicios | 2 | Falta `PLAYWRIGHT_MODULE` y Microsoft Edge en la selección ejecutada. |

La regresión completa **no está verde**. No se cambiaron aserciones ni módulos
ajenos a la migración para ocultar esos fallos. No se observaron fallos nuevos
atribuibles a MySQL 8.4 entre los casos ejecutados.

Evidencia local ignorada por Git: `TestResults/mysql84/mysql84.trx`,
`TestResults/mysql84/comparacion.json` y los TRX de comparación en
`TestResults/mysql80-comparacion/`. Los contenedores temporales de pruebas se
cerraron; no se eliminó ningún volumen de datos del taller.

## Comprobaciones pendientes de cierre

- Comprobación visual del historial y de una orden nueva desde la aplicación.
  La cuenta `admin` se creó con contraseña introducida en una terminal por el
  usuario; se verificaron su hash, rol y estado activo, conservando las dos cuentas
  importadas. El usuario confirmó posteriormente el acceso.
- Datos compartidos e independencia desde una segunda PC: el usuario aplazó
  esta comprobación para más adelante.
- Revisión humana del seed regenerado antes de incluirlo en un commit. Contiene
  datos ficticios y registros que ya existían en la base.

El seed generado se conserva solo en el equipo hasta su revisión. Los archivos
privados quedan fuera del commit.

## Carga de datos ficticios del 28 de octubre de 2026

El usuario autorizó poblar Aiven con datos de ejemplo para probar la aplicación y
la visibilidad compartida. Se guardó un respaldo previo
`respaldos/dump_nube_20261028_0128.sql` y uno posterior
`respaldos/dump_nube_20261028_0141.sql`.

Se añadieron 12 clientes, 18 vehículos, 6 mecánicos, 18 productos y 24 órdenes
ficticias, con 30 líneas de servicios y 27 líneas de productos. Hay 21 órdenes
activas y 3 anuladas. La carga está identificada por `demo_nube_v1`.

Los totales resultantes son 13 clientes, 19 vehículos, 8 mecánicos, 18 productos,
11 servicios y 24 órdenes. Se conservaron los registros previos y las 3 cuentas.
Diez servicios tienen identidad de catálogo y están disponibles para Nueva Orden.
Se adoptó la coincidencia única de Cambio de Aceite conservando su precio de 200;
los nuevos servicios respetan las unidades del catálogo. No se cambió código C#.

Antes de cargar Aiven, el SQL se aplicó dos veces en un MySQL 8.4 temporal sin red,
con `sql_require_primary_key=ON`. Los conteos y checksums de las doce tablas fueron
idénticos tras ambas ejecuciones. El contenedor temporal se cerró al finalizar.
En Aiven se comprobaron cero diferencias entre el total de cada orden ficticia
y sus detalles, y ausencia de stock negativo. El trigger generó tres registros
de ejemplo, además del historial anterior.

El seed posterior contiene 154 INSERT IGNORE y excluye Usuarios,
SolicitudesRegistro y Auditoria. No está aprobado para Git. Los auxiliares y la
evidencia de esta carga permanecen en `TestResults/datos-demo/`, ignorado por Git.

DBeaver Community se instaló desde su sitio oficial; la firma del ejecutable es
válida. El acceso directo `DBeaver - TallerMecanico` usa un workspace separado y
una conexión a Aiven con TLS requerido. Las credenciales las introduce el usuario;
la conexión visual queda pendiente de esa intervención.

## Prueba del seed poblado del 28 de octubre de 2026

Se probó el archivo real `scripts/bd/seed/datos_demo.sql`, con 154 INSERT IGNORE,
mediante dos ejecuciones de `aplicar-seed-local.ps1 -Contenedor <mysql-temporal>`.
SHA-256 del archivo probado:
`D177C59BC314932677B26B772B36FEC2FF5DFDCC6678C6B13C7BBA8401FE2642`.

El destino fue un MySQL 8.4 temporal sin red ni puertos publicados, con los datos
en tmpfs. Se prepararon las 12 tablas vacías a partir de la estructura exportada
anteriormente y se creó el trigger de historial. La prueba no se conectó a Aiven
ni reutilizó los volúmenes persistentes del taller. Se exigió clave primaria y se
comprobó que las restricciones de claves foráneas estaban habilitadas al verificar.

La primera carga dejó 13 clientes, 19 vehículos, 8 mecánicos, 18 productos,
11 servicios (10 con código de catálogo), 24 órdenes, 30 detalles de servicios,
27 detalles de productos y 4 registros de historial. Usuarios, SolicitudesRegistro
y Auditoria permanecieron vacías, tal como corresponde a las exclusiones del seed.

Pasaron 13 controles de integridad: referencias de vehículos, órdenes y detalles,
referencias del historial, totales de órdenes, subtotales de productos, stock no
negativo y presencia de servicios en las órdenes. La segunda carga conservó todos
los conteos y los checksums de las 12 tablas; no duplicó ni modificó sus filas.

Evidencia local ignorada por Git:
`TestResults/seed-local/20261028_120731_300-72cf644c/resultado.json`,
`primera.tsv` y `segunda.tsv` en esa misma carpeta. El servidor temporal se cerró
al finalizar y no se utilizó ningún volumen persistente. El MySQL local habitual
conserva sus datos anteriores; esta comprobación no cambia la web al modo offline.

## Carga del seed en el MySQL local habitual y arranque offline

El 28 de octubre de 2026, después de la prueba aislada, el usuario pidió probar
`datos_demo.sql` en el entorno local. Se inició únicamente el MySQL 8.4.13 del
proyecto `tallermecanico-local-84` y se comprobó que sus tablas de negocio estaban
vacías. Conservaba 2 usuarios y 3 registros de auditoría.

Antes de escribir se exportó el respaldo de las 12 tablas
`respaldos/dump_local_20261028_123101_487.sql`, con SHA-256
`BCB645E31ABB59AF9920775BE4855D7FEF8453F623F0DC93F8641CF3DCC5218E`.
Se cargó el mismo seed validado mediante `aplicar-seed-local.ps1` y se repitió
la carga. Los conteos y checksums de las 12 tablas coincidieron tras ambas
ejecuciones. Pasaron los 13 controles de integridad y se conservaron exactamente
los checksums previos de Usuarios, SolicitudesRegistro y Auditoria.

La base local quedó con los 154 registros de negocio: 13 clientes, 19 vehículos,
8 mecánicos, 18 productos, 11 servicios, 24 órdenes, 30 detalles de servicios,
27 detalles de productos y 4 registros de historial. Hay 10 servicios de catálogo.
Las 2 cuentas importadas y las 3 filas de auditoría se conservaron.

Se apagó el stack conectado a Aiven con `down` sin `-v` y se inició el stack local
completo usando las imágenes ya construidas. Se comprobó internamente, sin mostrar
credenciales, que las tres réplicas apuntan a `mysql:3306/taller_mecanico` en la red
local de Compose. Login, los endpoints de réplica y Mailpit respondieron HTTP 200;
Redis devolvió PONG. Los datos y los 13 controles se verificaron de nuevo después
del arranque. El stack queda ejecutándose en modo offline en
`http://127.0.0.1:8088`; Mailpit está en `http://127.0.0.1:8025`.

La cuenta `admin` creada en Aiven no se copió al MySQL local: el seed excluye
usuarios. El acceso visual requiere usar una cuenta local o crear cuentas locales
interactivamente. No se modificó la base de Aiven ni se utilizó el MySQL original
8.0, que permanece conservado.

Evidencia local ignorada por Git: `TestResults/seed-local/local-resultado.json`,
`local-antes.tsv`, `local-primera.tsv`, `local-segunda.tsv` y `local-web.tsv`.

## Lanzador de comandos diarios para Windows

El 28 de octubre de 2026 se añadieron `taller.cmd` y `scripts/entorno.ps1` para
agrupar los comandos de Docker existentes. No se añadió un gestor de paquetes de
JavaScript. El lanzador permite elegir `local` o `nube` y ejecutar `up`, `down`,
`seed`, `status`, `logs`, `build` o `usuarios`. La ayuda está en `.\taller`.

`up` valida la configuración, construye una vez la imagen común de las tres
réplicas, detiene el otro modo y espera respuestas HTTP 200. `down` solo detiene
los servicios elegidos, conservando contenedores y volúmenes. El MySQL original
8.0 queda excluido de los servicios de nube. `seed` solo se acepta en local y
delega al importador existente. `usuarios` conserva la consola interactiva para
que el humano introduzca las contraseñas.

Pasaron siete comprobaciones con Docker simulado: rechazo del seed en nube antes
de ejecutar Docker, elección correcta del Compose local, elección de los dos
Compose de nube, conservación del modo activo ante un build fallido, rechazo de
un nombre de proyecto alterado antes de escribir, destino exclusivamente local
del seed y apagado sin eliminar recursos ni afectar al MySQL original.

En Docker real se ejecutaron `local status`, `local up` y `local seed`. Los
conteos y checksums de las 12 tablas locales permanecieron idénticos. En esta
comprobación había 4 cuentas y 8 filas de auditoría, conservadas sin cambios.
Después se ejecutó `nube up`: las tres réplicas apuntaban a Aiven con TLS requerido
y Redis respondió PONG. Se volvió mediante `local up`, que dejó el modo local
activo y el de nube detenido; los checksums locales seguían idénticos y Redis
local devolvió PONG.

La comprobación de ambos modos incluye respuestas HTTP 200 en login a través de
las tres réplicas y Nginx. No se automatizó la creación de cuentas ni se ejecutó
`dotnet test` contra las bases de trabajo. La evidencia permanece ignorada por Git
en `TestResults/lanzador/resultado.txt`, `integracion.json`, `local-antes.tsv`,
`local-despues.tsv` y `local-tras-cambio.tsv`.

## Atajos ejecutados con dotnet y MSBuild

El 28 de octubre de 2026 se añadió `Directory.Build.targets` para ejecutar las
operaciones habituales mediante `dotnet msbuild -t:LocalUp`, `LocalSeed`,
`LocalDown`, `LocalStatus`, `LocalLogs`, `LocalBuild` y sus equivalentes de nube.
`Ayuda` enumera los comandos y `NubeSeed` devuelve un error antes de llamar a
Docker. Los targets reutilizan el script PowerShell existente y requieren
Windows, .NET SDK 10 y Docker Desktop; no se añadieron paquetes NuGet ni gestores
de paquetes de JavaScript.

Se comprobó con el SDK 10.0.401 que `Ayuda` funciona y `NubeSeed` falla como se
espera. La propiedad `MSBuildProjectDefaultTargets` sigue siendo `Build`; los
targets añadidos no tienen enganches de ejecución automática. En Docker real
pasaron `LocalStatus`, `NubeStatus`, `LocalUp` y `LocalSeed`. La publicación normal
de la aplicación dentro de la imagen Linux también terminó correctamente con el
nuevo archivo de targets presente.

Tras el arranque y el seed, los conteos y checksums de las 12 tablas locales
coincidieron con los anteriores. Se conservaron las 4 cuentas y las 8 filas de
auditoría. Los 13 controles de integridad devolvieron cero errores, login respondió
HTTP 200 y Redis devolvió PONG. El modo local quedó activo.

La evidencia de esta comprobación está ignorada por Git en
`TestResults/lanzador/dotnet/resultado.json`, `antes.tsv` y `despues.tsv`.

## Cierre posterior de las pruebas, 30/10/2026

Las cifras anteriores conservan su fecha y alcance histórico. Después de
corregir las expectativas y los recorridos de interfaz, aislar la persistencia
de las clases antiguas de autenticación y configurar Edge/PDF, la suite completa
terminó con **515 casos: 514 aprobados, 0 fallidos y 1 omitido**. La única omisión
es el envío real manual de Gmail. Los 13 casos fallidos de la reproducción del
29/10 quedaron aprobados, sin retirar ninguno de sus 505 casos.

La explicación reproducible y los límites del resultado actual están en
[VALIDACION_PRUEBAS.md](VALIDACION_PRUEBAS.md). Esta ejecución no utilizó Aiven
ni las bases de trabajo como servidor de pruebas.
