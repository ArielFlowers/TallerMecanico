# Validación de la migración a MySQL 8.4

La implementación parte de `c0bebbc` en `feature/entorno-remoto-compartido` y se
realiza en `feature/bd-nube`. Las comprobaciones comenzaron el 27 de octubre de
2026. Este informe distingue los resultados ejecutados de las comprobaciones
que requieren intervención humana o una segunda PC.

## Configuración y conservación de datos

- MySQL 8.4 en los cuatro Compose que incluyen base de datos y en los clientes
  de los scripts de nube. No hay referencias operativas a `mysql:8.0`.
- Los siete scripts coinciden con la sección 12 del plan recibido, salvo el
  cambio de imagen a `mysql:8.4`; tienen sintaxis válida y contenido ASCII para
  PowerShell 5.1.
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
- El seed se generó sin usuarios, solicitudes, auditoría ni `CREATE TABLE`.
  Tiene cero INSERT porque no hay datos de negocio. Se aplicó dos veces al MySQL
  local 8.4 con conteos idénticos; esta comprobación cubre el archivo vacío,
  no demuestra la repetición con datos de negocio representativos. Regenerarlo
  después de cargar datos de prueba y revisarlo antes de incorporarlo a Git.
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

- Login humano y edición de un costo con historial. El usuario no recuerda las
  contraseñas importadas; el administrador tiene un nombre distinto de `admin`,
  por lo que el restablecedor existente no puede actuar sobre esa cuenta. Las
  opciones de acceso con la consola actual están documentadas en `BD_NUBE.md`.
- Datos compartidos e independencia desde una segunda PC: el usuario aplazó
  esta comprobación para más adelante.
- Seed con datos de negocio representativos y revisión humana antes de incluirlo
  en un commit. El archivo generado está vacío.

El seed generado se conserva solo en el equipo hasta su revisión. Los archivos
privados quedan fuera del commit.
