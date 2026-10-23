# H2 Redis — Iteración 5

## Defectos reproducidos antes de corregir

Se ejecutaron procesos reales de la aplicación dentro de app3, con su conexión
explícita a `autotaller_h2_integracion` y sin publicar puertos adicionales:

| Configuración | Antes | Después |
|---|---|---|
| REDIS_HOST vacío, Redis:Host=redis | Redis deshabilitado por `??` | PONG; usa Redis:Host |
| TTL=0 | Arranca y acepta el valor | Rechazo antes de atender HTTP |
| TTL=-1 | Arranca y acepta el valor | Rechazo antes de atender HTTP |
| Ambos hosts vacíos | Redis deshabilitado | Se conserva; diagnóstico de sesiones locales |
| Redis configurado en puerto sin servidor | Sin diagnóstico de proveedor | NoDisponible y proveedor Redis, sin fallback local |

La auditoría HTTP anterior a los cambios también detuvo únicamente Redis H2:
el dashboard respondió 200/NoDisponible y consultó MySQL correctamente, pero leer
un borrador respondió 302 como si hubiera vencido y guardarlo respondió 302 como
si se hubiera almacenado. No eran datos expirados: el proveedor no estaba disponible.

## Cambios implementados

`ConfiguracionRedis.Leer` interpreta como ausente un host vacío o en blanco.
Un REDIS_HOST no vacío tiene precedencia; si falta se utiliza Redis:Host, respetando
la resolución de proveedores de IConfiguration. Un host no vacío pero inválido se
rechaza, sin esconderlo mediante una alternativa. Se aceptan DNS/IP, no URI,
credenciales ni un puerto incrustado. Puerto permitido: 1–65535.

El TTL del resumen debe ser un entero positivo. Solo la ausencia de la opción usa
60 segundos; cero, negativos, texto, fracciones y valores vacíos no se convierten
silenciosamente al valor por defecto. La validación precede a la conexión Redis y
al inicializador MySQL. No se modificaron los TTL de generación ni de sesión.

Después de construir la aplicación, cada réplica registra CONFIGURACION REDIS:
habilitado, estado observado, host, puerto, origen del host, TTL del resumen,
proveedor de sesiones y réplica. No registra HIT/MISS sin una lectura real ni
incluye credenciales/cadenas de conexión. Configurado pero desconectado continúa
usando RedisCache para sesiones; nunca cambia a memoria local durante un fallo.

`SesionDisponible` realiza LoadAsync explícito antes de usar los tres adaptadores
existentes de borradores/tokens. Esto propaga los errores que la carga síncrona
implícita de ISession podía tratar como sesión vacía. Los puertos siguen siendo
síncronos; se conserva su contrato sin expandir la refactorización a casos de uso.

`ConfirmarSesionFilter` confirma con CommitAsync únicamente las solicitudes Razor
que utilizaron esos adaptadores, antes de ejecutar la vista o redirección. Un fallo
de carga/confirmación devuelve 503 y un mensaje sin detalles técnicos; no aparenta
un borrador vencido ni una escritura exitosa. No enmascara errores de negocio y no
intercepta el dashboard cuando no utiliza esos adaptadores. El mensaje advierte
comprobar el resultado de un guardado antes de reenviarlo: MySQL puede haber confirmado
su operación antes de un fallo posterior de sesión. No hay rollback ni reintento
automático de transacciones.

Los errores propios se registran como SESION NO DISPONIBLE, separados de
RedisCacheAdapter. El middleware de sesiones del framework todavía puede registrar
sus propios errores al refrescar/cerrar una sesión; no se ocultaron esos eventos
ni se atribuyeron al dashboard. Las páginas de autenticación, sus cookies, roles,
auditoría y permisos no se modificaron. La corrección de confirmación explícita
se limita a los adaptadores de borradores/tokens, no pretende envolver todos los
usos futuros de ISession ni cambiar el comportamiento de nuevos logins durante una caída.

## Sesiones, cookies, Data Protection y expiraciones

- Cookie AutoTaller.Auth: autenticación mediante claims, 30 minutos y renovación
  deslizante; no depende de leer el contenido de sesión en cada petición.
- Cookie AutoTaller.Session: identificador protegido de la sesión. Los datos reales
  se almacenan en Redis mediante RedisCache, prefijo `autotaller:sesion:`.
- Las tres réplicas comparten ApplicationName=AutoTallerPro y el volumen de claves
  montado en /app/shared-keys. Se comprobaron cookies emitidas por A aceptadas por B/C.
- Antiforgery utiliza sus propios tokens/cookies protegidos por Data Protection,
  no el almacenamiento ISession. Un token emitido por A fue aceptado por B/C; un POST
  sin token devolvió 400. Un POST de formulario inválido sin uso de borradores siguió
  validando antiforgery durante la caída y no insertó datos.
- La protección del identificador/cookies no significa que el contenido de sesión
  en Redis esté cifrado por Data Protection.
- IdleTimeout de sesión continúa en 30 minutos. El Redis real mostró sldexp de
  18000000000 ticks y PTTL cercano a 1800000 ms. La actividad renueva ese TTL.
- Cada token de orden y sobre de borrador tiene, además, VenceUtc absoluto a 30 minutos.
  Leer la sesión no modifica ese vencimiento interno. Tampoco se cambiaron sus reglas.
- El resumen del dashboard conserva TTL absoluto de 60 segundos; la generación no
  expira (TTL=-1). No existe un TTL común para estas familias.

Para probar expiración sin esperar media hora se acortó solamente la clave de sesión
creada por el test y se verificó su renovación por actividad. Después se forzó su
expiración y se comprobó que un borrador no se recupera aunque la cookie de autenticación
siga siendo válida. Por separado, el auxiliar venció VenceUtc de los tres sobres
exclusivos de ese test y verificó su rechazo. Estas son pruebas controladas sobre Redis
real, no una espera natural de 30 minutos ni cambios a los tiempos de producción.

## Fallo y recuperación reales

Antes de detener Redis se verificaron etiquetas Compose, conexión MySQL de las tres
réplicas, red interna, miembros exclusivamente H2 y ausencia de puerto Redis publicado.
Solo se usaron `compose stop redis` y `compose start redis` del archivo tests/h2.
No hubo FLUSH, eliminación de volúmenes ni operaciones sobre infraestructura habitual.

Después de la corrección, con Redis detenido:

- Dashboard: 200, estado NoDisponible, un COUNT nuevo por cada tabla y sin HIT/MISS falso.
- Lectura y guardado de borradores: 503, sin stack técnico expuesto ni falsa redirección.
- Cookies ya emitidas: siguen autorizando las consultas comprobadas; no se desactivó
  antiforgery ni se introdujo un proveedor alternativo de sesiones.

Al arrancar Redis las tres réplicas recuperaron PONG sin reiniciarse. El siguiente
MISS hizo los tres COUNT y el HIT cruzado no los repitió. Se reconstruyó una generación
nueva válida, con TTL=-1. Los borradores/tokens previos al reinicio se perdieron y no
se inventaron ni recuperaron desde MySQL; los nuevos volvieron a compartirse entre
A/B/C y por Nginx. La prueba de sesiones no creó órdenes ni cambió stock/entidades.

## Política de memoria: riesgos y propuestas, no cambios automáticos

Compose habitual y H2 conservan 128 MiB, allkeys-lru, save vacío y appendonly=no.
La política puede expulsar cualquier clave, incluso sesiones y generación. Los prefijos
no dan prioridad ni aislamiento de memoria. El resumen se regenera; un borrador no.
La pérdida de generación se maneja de forma segura por la Iteración 4, pero eso no
restaura sesiones. Un reinicio del Redis volátil pierde todo su estado; se comprobó
realmente y no se presenta la reconexión como recuperación de los borradores perdidos.

No se forzó una saturación de 128 MiB: el riesgo de expulsión se deriva de la política
real observada. La pérdida por reinicio sí fue reproducida. No se cambió política,
persistencia, recursos ni instancias.

Alternativas que requieren autorización:

1. Redis único con noeviction y AOF: reduce pérdida por expulsión/reinicio, pero al
   llenarse rechaza escrituras; AOF tiene su propio coste y ventana de durabilidad.
   No elimina el acoplamiento de disponibilidad entre caché y sesiones.
2. Separar sesiones en Redis con política de durabilidad/memoria propia y mantener
   la caché descartable en el actual: aísla responsabilidades y presión de memoria,
   pero añade infraestructura y configuración. No se creó una segunda instancia.

No se promete frescura estricta MySQL/Redis ante una invalidación fallida ni una
transacción distribuida. La ventana GetById/Delete sigue siendo un pendiente separado.
El contenido de sesión también sigue siendo susceptible a escrituras concurrentes
de la misma sesión; no se añadió control de versiones para borradores en esta iteración.

La auditoría de Nginx encontró confianza de ForwardedHeaders fijada a 172.19.0.0/16
en Program.cs, mientras la red aislada H2 observada usa 172.18.0.0/16. Las pruebas
H2 usan HTTP/Development y no certifican el procesamiento HTTPS de cabeceras del
entorno habitual. Se debe verificar/configurar la red del proxy antes de otro
despliegue; no se amplió la confianza automáticamente ni se cambió autenticación.

## Archivos de esta iteración

Producción nuevos:

- Infraestructura/Configuracion/ConfiguracionRedis.cs
- Infraestructura/Sesion/SesionDisponible.cs
- Infraestructura/Sesion/ConfirmarSesionFilter.cs

Producción modificados:

- Program.cs (configuración validada, diagnóstico y registro del filtro)
- Infraestructura/Sesion/TokenOrdenSesion.cs (carga explícita)
- Infraestructura/Sesion/BorradorOrdenSesion.cs (carga explícita)
- Infraestructura/Sesion/BorradorVehiculoSesion.cs (carga explícita)

Pruebas/documentación nuevos:

- tests/TallerMecanico.IntegrationTests/ConfiguracionRedisTests.cs
- tests/TallerMecanico.IntegrationTests/SesionDisponibleTests.cs
- tests/h2/SesionRedisProbe/SesionRedisProbe.csproj
- tests/h2/SesionRedisProbe/Program.cs
- tests/h2/sesiones_real.cjs
- tests/h2/INFORME-ITERACION5.md
- tests/h2/resultado-iteracion5.json

Documentación modificada: tests/h2/README.md.

No se modificaron appsettings, Compose, Nginx, esquema/SQL, caché concurrente,
CRUD, páginas de negocio/autenticación, búsquedas ni transacciones.

## Reproducción segura

Con la imagen actual desplegada solo en H2 y PLAYWRIGHT_MODULE configurado:

```powershell
dotnet restore tests/h2/SesionRedisProbe/SesionRedisProbe.csproj
dotnet publish tests/h2/SesionRedisProbe/SesionRedisProbe.csproj -c Release --no-restore --no-self-contained -p:UseAppHost=false -o tests/h2/SesionRedisProbe/bin/publicado
docker compose --env-file tests/h2/local.env -f tests/h2/docker-compose.yml cp tests/h2/SesionRedisProbe/bin/publicado/. app3:/tmp/h2-sesiones
docker compose --env-file tests/h2/local.env -f tests/h2/docker-compose.yml exec -T app3 dotnet /tmp/h2-sesiones/SesionRedisProbe.dll configuracion
node tests/h2/sesiones_real.cjs
```

ADVERTENCIA: la última prueba detiene/arranca exclusivamente Redis H2 y pierde las
sesiones volátiles de ese entorno. El script vuelve a comprobar el aislamiento y
lo arranca en finally si ocurre un fallo. No ejecutar mientras otro test H2 requiera
conservar su sesión. El modo --auditoria registra el comportamiento sin exigir el
503 de la versión corregida; se utilizó antes de los cambios.

El auxiliar de sesión utiliza ISessionStore, los adaptadores reales y el proveedor
Redis real dentro de app3; recibe identificadores solo por stdin y no los registra.
No agrega endpoints de depuración a producción. Sus procesos de prueba de arranque
se ejecutan en un puerto interno, se cierran por su propio PID y no reemplazan el
proceso servidor principal de app3.

## Resultado final de verificación

| Verificación | Aprobadas | Fallidas | Omitidas |
|---|---:|---:|---:|
| Configuración .NET | 33 | 0 | 0 |
| Confirmación/carga de sesión .NET | 8 | 0 | 0 |
| Caché, invalidación y concurrencia .NET anteriores | 72 | 0 | 0 |
| Regresión segura conjunta (incluye las anteriores) | 363 | 0 | 107 |

Build final: 0 errores, 0 advertencias. Las 107 omitidas no se presentan como
aprobadas. Se excluyeron, además, seis clases que pueden usar MySQL local o correo
real; no forman parte de las 470 seleccionadas de la regresión segura.

Integración real: cinco escenarios de arranque, la suite de sesiones/borradores/
caída/recuperación, cinco escenarios sincronizados de concurrencia, seis altas/bajas,
diez casos sin invalidación y la suite de caché/expiración pasaron. Los conteos del
MISS normal fueron 85→86 en cada tabla; HIT permaneció en 86. PTTL: 58307→55733 ms,
luego TTL=-2 por expiración natural. La generación permaneció idéntica con TTL=-1.

Durante la caída corregida: dashboard 200, lectura de borrador 503, guardado de
borrador 503. No hubo errores técnicos expuestos en esas respuestas. Tras recuperar
Redis, cada COUNT pasó de 64 a 65 y el HIT quedó en 65; los borradores nuevos fueron
recuperables. MySQL, Redis y Nginx conservaron sus contenedores; el contenedor ajeno
permaneció detenido. Solo se reinició Redis H2, dos veces: antes y después de corregir.

Los fallos de commit posteriores a carga se comprobaron con dobles, no se atribuyen
a una reproducción real de ese intercalado. El primer arnés tuvo siete fallos de
configuración de PageContext (RouteData ausente), corregidos antes de la ejecución
final. La prueba real también ajustó la detección de la clave: Login ya almacena
Username, por eso se toma la referencia antes de iniciar sesión. No se alteró
producción para debilitar aserciones.

La evidencia completa anterior/posterior está en resultado-iteracion5.json; no
incluye cookies, contraseñas, claves de sesión ni tokens de formularios. Se conservó
el cliente fixture H2 Id=4 y el historial generado por las pruebas CRUD, sin eliminar
datos previos. No se requiere ejecutar migraciones ni cambiar .env para estos cambios.

Commit propuesto, no realizado:

`fix(redis): fortalecer configuracion y resiliencia de sesiones`
