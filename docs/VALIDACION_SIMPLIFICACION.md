> **Registro fechado anterior a la retirada del modo local.** Los resultados y comandos históricos se conservan como evidencia. El flujo vigente usa solo Aiven; consultar [README.md](../README.md) y [VALIDACION_NUBE_UNICA.md](VALIDACION_NUBE_UNICA.md).

# Validación de la simplificación - 29/10/2026

Se conservaron tres réplicas, Nginx, HTTPS, Redis y MySQL 8.4, sin cambiar C#,
Razor, Dockerfile, nginx.conf ni la lógica de negocio. La instalación y el uso
diario se explican en [README.md](../README.md). Los resultados del 27-28/10/2026
siguen en [VALIDACION_MYSQL84.md](VALIDACION_MYSQL84.md), con sus fechas y límites.

## Configuración y comandos

- Se comparó el JSON efectivo anterior (Compose de nube más overlay de build)
  con el actual, usando credenciales ficticias. Coinciden nombre de proyecto,
  servicios completos, puertos, conexiones, redes y volúmenes. También coinciden
  al personalizar las cuatro variables APP anteriores. La plantilla tiene nueve
  variables activas y el Compose local permanece idéntico.
- Se ejecutaron siete escenarios del lanzador con Docker simulado: rechazo de
  seed en nube antes de Docker, selección de cada Compose, cambio automático,
  build fallido sin detener el modo activo, rechazo de proyecto alterado, destino
  local de seed y apagado por proyecto **y servicio** sin afectar al MySQL original.
  Cada arranque conserva las cuatro comprobaciones HTTP.
- En ejecución real pasaron `Ayuda`, `LocalStatus`, `NubeStatus` y `LocalUp`
  mediante `dotnet msbuild`. `NubeSeed` devolvió el error previsto. Se conservan
  los targets y el lanzador `.\taller`, incluidas las cuentas interactivas.
- El build Docker terminó correctamente. `LocalUp` mostró en la salida clásica
  de MSBuild el enlace final de aplicación y Mailpit. No se automatizó la creación
  de cuentas ni se ejecutó la suite de negocio contra bases de trabajo.

## Seed ficticia aislada

Se probó el importador real de PowerShell contra MySQL 8.4, en un contenedor
temporal **sin red, sin puertos publicados y con datos en tmpfs**. El esquema de
las doce tablas se obtuvo sin datos del MySQL local; no se copió identidad real.

| Tabla de negocio | Filas nuevas |
|---|---:|
| Clientes | 5 |
| Vehiculos | 8 |
| Mecanicos | 4 |
| Productos | 8 |
| Servicios | 10 |
| Ordenes | 6 |
| DetalleServiciosOrdenes | 8 |
| DetalleOrdenes | 6 |
| HistorialCostoServicios | 2 |
| **Total** | **57** |

- Primera carga correcta, con cinco órdenes activas, una anulada y los diez
  servicios del catálogo. Se verificaron relaciones, totales de órdenes,
  subtotales, unidades, stock no negativo y presencia de servicios por orden.
- La segunda ejecución reconoció `Clientes.Id=1`, `CreadoPor=seed_demo_v2` y
  terminó sin escribir; todos los conteos y checksums coincidieron.
- Una fila de producto de otro origen hizo rechazar la carga, conservando
  íntegros los conteos y checksums de esa base.
- Una restricción adicional provocó un error en Productos después de insertar
  Clientes, Vehiculos y Mecanicos. La transacción revirtió todas esas inserciones.
- Una cuenta ficticia previa y las tablas SolicitudesRegistro y Auditoria
  conservaron sus checksums. Las claves foráneas permanecieron activas; el SQL
  no contiene DDL, `INSERT IGNORE` ni desactivación de restricciones.
- El contenedor temporal se eliminó al finalizar. La carga inicial debe hacerse
  sin altas de negocio ni otra carga simultánea.

SHA-256 de la nueva seed:
`402001CE0231A9F2FED02871F4F69AB0DDEE508024BF2DDECCC2D5C83B5A4F1F`.

La exportación anterior queda privada en
`respaldos/seed_exportada_aiven_20261029.sql`, ignorada por Git. Su SHA-256 conserva
la huella histórica `D177C59BC314932677B26B772B36FEC2FF5DFDCC6678C6B13C7BBA8401FE2642`.
Se retiró el generador desde Aiven; se mantienen los scripts administrativos
separados de respaldo, exportación e importación.

## Datos existentes y funcionamiento local

Antes y después de construir, arrancar y solicitar `LocalSeed`, coincidieron
conteos y checksums de **las doce tablas locales**. La nueva seed fue rechazada
por tratarse de datos de otro origen, sin copiar ni ejecutar su archivo SQL.

La base habitual conserva sus 154 registros de negocio, 4 cuentas, cero
solicitudes y 8 auditorías. El MySQL original 8.0 permanece detenido, con su
volumen conservado. `.env` y `bd-nube.env` mantienen sus hashes anteriores.

| Comprobación real | Resultado |
|---|---|
| Login directo en 5000, 5001 y 5002 | HTTP 200 en las tres réplicas |
| Login por Nginx en 80 y 8088 | HTTP 200 |
| Doce peticiones a `/h1/replica` por Nginx | Tres identificadores distintos |
| `/h2/redis` en tres réplicas y Nginx | PONG |
| `redis-cli ping` | PONG |
| Mailpit en 8025 | HTTP 200 |
| Login HTTPS en 443 | HTTP 200; raíz mkcert confiable en Windows |

La primera comprobación conservó el certificado autofirmado del 27/10 y usó
`curl --cacert nginx/certs/localhost.pem`. Ese ensayo detectó una raíz no confiable;
no cambió los certificados ni el almacén de Windows.

Después, el 29/10/2026, por petición del usuario, se instaló mkcert 1.4.4 para su
cuenta, se ejecutó `mkcert -install` y se generaron nuevos PEM para `localhost`,
`127.0.0.1` y `::1`. La raíz aparece en `Cert:\CurrentUser\Root`. Se guardaron los
certificados anteriores en `respaldos/certificados_antes_mkcert_20261029_*/`,
privado e ignorado por Git. `nginx -t` pasó y se recargó Nginx sin cambiar nginx.conf.

`Invoke-WebRequest` devolvió HTTP 200 en `https://localhost/Auth/Login` y
`https://127.0.0.1/Auth/Login` con la validación normal de Windows, sin callbacks
ni excepciones de confianza. Curl Schannel también devolvió 200 sin `--cacert`
ni `--insecure`, usando `--ssl-revoke-best-effort`: la CA local no publica una
lista de revocación. Ese indicador conserva la validación de la cadena y el
nombre y tolera puntos de revocación ausentes; ver el
[manual de curl](https://curl.se/docs/manpage.html#--ssl-revoke-best-effort).
La comprobación se registró en `TestResults/mkcert/resultado.json`.

El modo local queda activo en http://127.0.0.1:8088. No se inició el modo nube,
no se conectó a Aiven y no se ejecutaron pruebas ni importaciones contra él.
La equivalencia de nube se comprobó por configuración y simulación del lanzador;
no constituye una nueva validación en vivo de Aiven o de una segunda PC. El usuario confirmó posteriormente que la comprobación compartida con un compañero ya se había realizado antes de esta simplificación.

## Documentación y evidencia

Las dos guías PDF se elaboraron en esta revisión y se retiraron posteriormente
por decisión del equipo. El README quedó como guía principal; los registros
históricos y sus límites se conservan en Markdown. La documentación de recuperación queda
en [BD_NUBE.md](BD_NUBE.md); el plan de anfitrión/Tailscale se marca como histórico.

La evidencia local está ignorada por Git en `TestResults/simplificacion/`:
`verificar.py`, `validar-lanzador.ps1`, `verificar-live.py`, `integracion.json`,
`seed-primera.tsv`, `integridad.tsv`, `local-antes.tsv`, `local-despues.tsv` y
los logs de MSBuild. Esos auxiliares no son dependencias del proyecto.

El cierre posterior de la suite, del 30/10/2026, consta en
[VALIDACION_PRUEBAS.md](VALIDACION_PRUEBAS.md): 514 aprobadas, cero fallidas y una
omisión manual de Gmail real, con MySQL, Edge y PDF habilitados. Se conservan las
fechas y los resultados históricos del informe de migración.
