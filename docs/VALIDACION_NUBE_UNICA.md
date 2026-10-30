# Aiven como única base de trabajo

Implementación y verificación del **30/10/2026**. El único arranque diario es
`dotnet msbuild -t:NubeUp`. Se mantienen tres réplicas, Nginx, HTTPS, Redis,
MySQL 8.4 en Aiven y las pruebas automáticas en servidores temporales aislados.

## Cambios aplicados

- Retirados los Compose local y anfitrión/Tailscale, el SQL de demostración y
  su importador. El generador de seed desde Aiven ya se había retirado.
- Eliminados todos los targets `Local*`, `NubeSeed`, la acción `seed` y el cambio
  automático entre modos. Continúan `NubeUp`, `NubeDown`, `NubeStatus`,
  `NubeLogs`, `NubeBuild`, `Ayuda` y `.\taller nube usuarios`.
- El lanzador usa un solo Compose, proyecto y conjunto de servicios. Conserva
  construcción previa, filtros por proyecto y servicio, cuatro comprobaciones
  HTTP y enlace final visible.
- La plantilla tiene **7 variables**. Se retiraron las dos contraseñas locales;
  los `.env` existentes y las opciones compatibles se conservan.
- Retirada la conexión local con contraseña de ejemplo de `appsettings.json`.
  Nginx envía su propio esquema HTTP/HTTPS, sin adaptación para Tailscale.
- Se mantienen esquema, migraciones, catálogo y cuentas. No se modificó C#,
  Razor, reglas de negocio ni el esquema público de la aplicación.
- Los scripts de respaldo, restauración y diagnóstico continúan separados.
  El exportador local queda para recuperar recursos históricos, fuera del uso diario.

## Resultado comprobado

| Comprobación | Resultado |
|---|---|
| Compose efectivo anterior/nuevo, con valores ficticios | Idénticos: servicios, conexiones, puertos, volúmenes y build |
| Variables visibles de la plantilla | 7 |
| Configuración privada y PEM | Hashes de `.env`, `bd-nube.env` y ambos certificados iguales |
| Siete escenarios simulados del lanzador | Aprobados: rechazo de comandos retirados, orden de build/up, fallo de build, proyecto alterado, apagado seguro, auxiliares y status |
| Targets públicos | Exactamente Ayuda y los cinco targets Nube de operación |
| Build Linux y `NubeUp` real | Correctos; enlace final visible |
| Login directo, puertos 5000/5001/5002 | HTTP 200 en las tres réplicas |
| Login por Nginx, puertos 80 y 8088 | HTTP 200 |
| HTTPS localhost y 127.0.0.1 | HTTP 200 con validación normal de Windows, sin excepciones de confianza |
| Doce peticiones a `/h1/replica` por Nginx | Tres identificadores de réplica distintos |
| Redis CLI y `/h2/redis` en las tres réplicas y Nginx | PONG |
| `nginx -t`, Ayuda y NubeStatus | Correctos |
| Doce tablas de Aiven antes/después | Conteos y checksums idénticos, incluidas identidad y auditoría |
| Suite completa, con MySQL, Edge y PDF habilitados | **515 casos: 514 aprobados, 0 fallidos y 1 omitido**; 5 min 30 s |

La única prueba omitida es el envío real de Gmail, que requiere autorización y
destinatario explícitos. No se enviaron correos reales. La suite usó únicamente
el MySQL temporal propio; no se ejecutó `dotnet test` contra Aiven.

## Incidencias observadas y límites

La primera ejecución de la suite registró un timeout HTTP de cinco segundos
durante un login mientras también se construía la imagen Docker. La repetición
sin compilar imágenes en paralelo aprobó los 514 casos ejecutables, sin cambiar
las pruebas ni retirar casos. La concurrencia es una posible explicación del
timeout; no se atribuye a una regresión funcional demostrada.

El primer `NubeUp` agotó su espera HTTP y las réplicas registraron reintentos SQL
y un timeout del bloqueo de esquema. Se comprobó que la conexión de las apps
coincide con la administrativa y exige TLS. Las réplicas terminaron disponibles;
la repetición de `NubeUp`, con reconstrucción y recreación de apps, concluyó
correctamente. Se conserva la espera de 90 segundos por dirección y el error
explícito si no responde; no se anuncia éxito antes de las cuatro respuestas 200.

Las comprobaciones administrativas de Aiven fueron de lectura. El arranque de
la aplicación conserva su inicialización habitual del esquema. No se importaron
dumps ni se ejecutaron altas, ediciones o pruebas destructivas sobre sus datos.
Esto no valida carga, failover SQL ni disponibilidad futura del proveedor.

## Conservación de recursos y evidencia

El entorno local anterior ya estaba detenido al iniciar la transición. Sus
contenedores y volúmenes permanecen conservados; también el MySQL original de
la migración. El estado final deja activos los cinco servicios de nube.
No se trasladaron datos locales a Aiven.

Al terminar no quedaba ninguna base de fixture en el servidor temporal. Se
retiró únicamente el contenedor de pruebas creado para esta verificación.
Los archivos privados y los respaldos anteriores siguen fuera de Git.

La evidencia privada está ignorada en `TestResults/nube-unica/`:
`configuracion.json`, `lanzador.json`, `completa.trx`, `completa-final.trx`,
`build.log`, `up.log`, `integracion.json`, `conservacion-aiven.json`,
`aiven-antes.tsv`, `aiven-despues.tsv` y `limpieza.json`.
Por decisión del equipo, las dos guías PDF y la carpeta de salida se retiraron.
El README concentra instalación y uso diario; la documentación Markdown conserva
administración, pruebas y registros históricos con sus fechas.

Instalación: [README.md](../README.md). Administración y recuperación:
[BD_NUBE.md](BD_NUBE.md). Preparación de tests: [tests/README.md](../tests/README.md).
