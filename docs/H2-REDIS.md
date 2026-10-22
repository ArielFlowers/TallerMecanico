# H2 - Redis como caché distribuida

## 1. Descripción

Las tres réplicas de AutoTaller Pro (app1, app2 y app3) comparten
una única instancia de Redis. Redis se usa para:

- Caché del resumen del dashboard (patrón cache-aside).
- Sesión distribuida (`IDistributedCache`), para que los borradores
  y tokens de órdenes sigan disponibles aunque Nginx envíe la
  siguiente solicitud a otra réplica.

## 2. Arquitectura

- Puerto: `Aplicacion/Puertos/ICacheDistribuidaPort.cs`
- Adaptador Redis: `Infraestructura/Cache/RedisCacheAdapter.cs`
  (StackExchange.Redis, `SET clave valor EX segundos`).
- Adaptador sin Redis: `Infraestructura/Cache/CacheDistribuidaDeshabilitada.cs`
  (cuando no existe `REDIS_HOST`, por ejemplo `dotnet run` sin Docker).
- Servicio cache-aside: `Aplicacion/Servicios/DashboardCacheService.cs`
- Clave: `autotaller:dashboard:resumen` (`Aplicacion/Servicios/ClavesCache.cs`).
- Vista: `Pages/Index.cshtml` muestra `Cache: HIT`, `Cache: MISS` o
  `Cache: NO DISPONIBLE`, la réplica que generó el dato, la réplica que
  atendió la solicitud y los segundos restantes antes de expirar.

Flujo:

    PageModel (Index) -> DashboardCacheService -> ICacheDistribuidaPort (Redis)
                                               -> DashboardService -> Repositorios ADO.NET -> MySQL

- MISS: Redis no tiene la clave, se consulta MySQL y se guarda el
  resultado en Redis con expiración absoluta. Log: `MISS -> MySQL -> Redis`.
- HIT: Redis devuelve el valor sin consultar MySQL. Log: `HIT -> Redis`.
- Si Redis no responde, se consulta MySQL y la página sigue funcionando.
  Log: `MISS -> MySQL | Redis no disponible`.

## 3. Configuración

Docker Compose define para las tres réplicas:

| Variable | Valor | Uso |
|---|---|---|
| `REDIS_HOST` | `redis` | Servicio Redis dentro de la red de Docker |
| `REDIS_PORT` | `6379` | Puerto de Redis |
| `Redis__ExpiracionDashboardSegundos` | `60` | Expiración absoluta de la clave del dashboard |

El servicio `redis` usa `redis:7.4-alpine`, sin persistencia en disco,
con `maxmemory 128mb` y `allkeys-lru`. Se publica solo en
`127.0.0.1:6379` para pruebas locales.

Para ejecutar con `dotnet run` contra el Redis de Docker:

    docker compose up -d redis mysql
    REDIS_HOST=localhost dotnet run

## 4. Levantar el sistema

    docker compose up -d --build
    docker compose ps

## 5. Evidencias

### Redis operativo e integrado

    docker exec -it taller-mecanico-redis redis-cli ping

Desde cada réplica (equivalente a `redis-cli ping`, solo en Development):

    curl http://127.0.0.1:5000/h2/redis
    curl http://127.0.0.1:5001/h2/redis
    curl http://127.0.0.1:5002/h2/redis

Cada respuesta incluye la réplica, `"redis": "PONG"` y la latencia.

### MISS, MySQL y almacenamiento en Redis

1. Iniciar sesión y abrir `http://127.0.0.1:5000/`.
2. La primera consulta muestra `Cache: MISS`.
3. Log: `docker compose logs app1 | grep MISS`

        MISS -> MySQL -> Redis | clave autotaller:dashboard:resumen | réplica <id> | expira en 60 s

4. Inspeccionar el valor:

        docker exec -it taller-mecanico-redis redis-cli GET autotaller:dashboard:resumen
        docker exec -it taller-mecanico-redis redis-cli TTL autotaller:dashboard:resumen

### HIT y recuperación desde Redis

Recargar la página: muestra `Cache: HIT`.

    docker compose logs app1 | grep HIT

### Caché compartida entre réplicas

1. Abrir `http://127.0.0.1:5000/` (MISS, generado por la réplica 1).
2. Abrir `http://127.0.0.1:5001/` y `http://127.0.0.1:5002/`:
   muestran `Cache: HIT`, "Generado por" la réplica 1 y
   "Atendido por" la réplica actual.
3. Crear o eliminar un mecánico, vehículo o servicio desde cualquier
   réplica: la clave se invalida y la siguiente lectura en otra réplica
   es `MISS` con el conteo actualizado desde MySQL; las demás obtienen `HIT`
   con el nuevo valor.

### Expiración e invalidación

- Expiración: esperar 60 segundos (o el valor configurado). La siguiente
  consulta vuelve a ser `MISS` y consulta MySQL.
- Invalidación manual por consola:

        docker exec -it taller-mecanico-redis redis-cli DEL autotaller:dashboard:resumen

- Invalidación desde la aplicación: el Administrador dispone del botón
  "Invalidar caché" en el inicio. Log:

        INVALIDACION -> Redis DEL autotaller:dashboard:resumen | eliminado: True

### Continuidad sin Redis

    docker compose stop redis

El inicio muestra `Cache: NO DISPONIBLE` y los datos se leen de MySQL.
Al ejecutar `docker compose start redis` la caché vuelve a `MISS` y luego `HIT`.

## 6. Pruebas automatizadas

    dotnet test tests/TallerMecanico.IntegrationTests --filter FullyQualifiedName~DashboardCache

Cubren MISS, HIT, caché compartida, invalidación, expiración,
valores corruptos y Redis no disponible.
