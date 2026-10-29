# Plan y handoff: entorno remoto compartido

> Fecha: 23/10/2026
> Rama creada: `feature/entorno-remoto-compartido`
> Base: `main` en `681c6f7` (merge del PR #40)
> Estado: **anfitrión Tailscale operativo, Gmail activo y flujo de registro verificado; falta migrar el dump más completo, publicar la imagen canónica y crear el administrador**.
> Este archivo es el punto de continuación para otro modelo/sesión.

## 0. Progreso de ejecución (actualizado 23/10/2026)

Implementado y verificado:

- [x] Rama `feature/entorno-remoto-compartido`.
- [x] `main` actualizado a `681c6f7` (PR #40); conflicto del autostash resuelto en `DatabaseInitializer.cs`.
- [x] SMTP genérico en `GmailEmailService`: host/puerto libres, `Email:Smtp:Security`, autenticación opcional.
- [x] `App:PermitirHttpNoSeguro` en `EnlaceVerificacion` (desactivado por defecto).
- [x] Lock `GET_LOCK`/`RELEASE_LOCK` en `DatabaseInitializer`.
- [x] Reintentos de arranque en `Program.cs` con `Database:IntentosInicio` y `Database:EsperaEntreIntentosSegundos`.
- [x] `docker-compose.yml` con imagen canónica, MySQL remoto y sin servicio MySQL local.
- [x] `docker-compose.host.yml`, `docker-compose.local.yml`, `docker-compose.build.yml`.
- [x] `.env.example`, `.dockerignore`, `Dockerfile` con imágenes base parametrizadas.
- [x] README, `docs/migraciones/README.md`, `tests/README.md`.
- [x] Build de la imagen con .NET 10 en Docker.
- [x] 70 pruebas post-merge: `GmailEmailService` (21), `RegistroFacade`/`ReenvioVerificacion` (19) y `UsuarioAdministracionMySql` (30).
- [x] `docker compose config` válido para los 4 compose.
- [x] Tailscale 1.104.2 instalado; nodo `taller-db`, IP `100.99.175.117`, DNS `taller-db.tail590083.ts.net`.
- [x] Conectividad Docker → Tailscale validada en `3307`, `1026` y `8026`.
- [x] `.env` real creado (ignorado por Git) con puertos `3307`/`1026`/`8026`.
- [x] `docker-compose.host.yml` activo: MySQL 8 y Mailpit saludables.
- [x] Stack de 3 réplicas + Redis + Nginx activo; Nginx en `127.0.0.1:8088`.
- [x] Tailscale Serve publica `https://taller-db.tail590083.ts.net` hacia `127.0.0.1:8088`.
- [x] Registro → correo en Mailpit → enlace HTTPS → verificación probado de punta a punta.
- [x] Gmail activo con contraseña de aplicación; login SMTP (465) e IMAP validados.
- [x] Registro → correo real en Gmail → enlace HTTPS → verificación probado de punta a punta.
- [x] Esquema creado en la base nueva: 12 tablas, trigger `TRG_Servicios_HistorialCosto`.

Pendiente:

- [ ] Exportar/importar el dump de la base más completa; la base actual está recién inicializada.
- [ ] Construir y publicar la imagen canónica; fijar digest en `APP_IMAGE`.
- [ ] Crear Administrador/Recepcionista con `--inicializar-usuarios` (requiere consola interactiva).
- [ ] Verificación funcional desde una segunda máquina (solo se probó desde el anfitrión).
- [ ] Crear `AGENTS.md`.
- [ ] Push de la rama (no solicitado todavía).

---

## 1. Decisiones acordadas

1. **Una única base MySQL remota** alojada en la PC de un integrante, accesible por Tailscale.
2. **Mailpit compartido** en esa misma PC anfitriona para los correos de verificación.
3. **Una imagen Docker canónica** publicada en GHCR o Docker Hub, referenciada por digest.
4. La base remota se inicializa **importando un dump de la base local más completa**.
5. Los tests nunca deben apuntar a la base remota; siguen usando `TALLER_TEST_MYSQL` local.

---

## 2. Estado actual del repositorio

- Rama actual: `feature/entorno-remoto-compartido`, actualizada por fast-forward a `main` `681c6f7`.
- `main` ya incluye el PR #40 (`refactor-buscadores`); el autostash se aplicó y su único conflicto (`DatabaseInitializer.cs`) quedó resuelto. El stash temporal fue eliminado.
- Los cambios de código, compose y documentación se consolidan en un commit local; no se hace push.
- Existe `.env` local (ignorado por Git) con la configuración del anfitrión; no debe compartirse.
- Existe `nginx/certs/` con certificado autofirmado local (ignorado por Git); Tailscale Serve usa su propio certificado HTTPS.
- El compose principal ya no incluye MySQL; el anfitrión ejecuta `docker-compose.host.yml` y el stack web con `docker-compose.yml + docker-compose.build.yml`.

---

## 3. Diagnóstico del problema de registro/verificación

Mensaje visible: **“El registro no está disponible en este momento. Intenta más tarde.”**

Flujo real:

1. `Pages/Auth/Registro.cshtml.cs:61-69` captura `ConfiguracionRegistroException`.
2. `Aplicacion/Fachadas/RegistroFacade.cs:34-36` valida antes de guardar:
   - `App:PublicBaseUrl`
   - configuración SMTP
3. `Aplicacion/Servicios/EnlaceVerificacion.cs:9-18` exige:
   - HTTPS, o
   - HTTP solo si es loopback (`127.0.0.1`/`localhost`)
4. `Infraestructura/Correo/GmailEmailService.cs:134-153` exige actualmente:
   - host exacto `smtp.gmail.com`
   - puerto exacto `465`
   - usuario y contraseña no vacíos
   - remitente válido

Causa principal: la configuración SMTP/URL **no estaba en `appsettings.json` ni en el compose**. Solo estaba en los User Secrets de la máquina de Aldair. Los User Secrets son locales y no entran a Docker. `.dockerignore:15-18` además excluye `secrets.json` y `appsettings.Development.json`.

Alex ya corrigió la propagación por variables de entorno en Docker, pero **todavía falta**:

- Un Mailpit compartido o credenciales Gmail compartidas.
- SMTP genérico (hoy está hardcodeado a Gmail).
- URL pública alcanzable por Tailscale.
- Base remota.
- Imagen canónica.
- Migración de datos.
- Lock de inicialización.
- Reintentos cuando la base remota no responde.

---

## 4. Diagnóstico del problema de Docker

### 4.1 Imágenes distintas

- `docker-compose.yml:9-13` usa `build: .` e `image: taller-mecanico:h1`.
- Cada integrante compila desde su propia rama/estado de trabajo.
- El tag `taller-mecanico:h1` es local y mutable.
- No hay registry ni digest fijo.
- Las imágenes base usan tags flotantes:
  - `mcr.microsoft.com/dotnet/sdk:10.0`
  - `mcr.microsoft.com/dotnet/aspnet:10.0`
  - `mysql:8.0`
  - `redis:7.4-alpine`
  - `nginx:1.27-alpine`
- `.dockerignore` no excluye documentación ni archivos locales no versionados, así que el contexto de build cambia entre máquinas.

### 4.2 Bases de datos distintas

- `docker-compose.yml:64` usa el volumen local `taller-mysql-data`.
- El volumen vive en cada máquina.
- El nombre del proyecto/volumen depende de la carpeta de clonación.
- No hay semilla ni dump reproducible; solo existe `--inicializar-usuarios` interactivo (`Program.cs:483`).
- Por eso cada integrante tiene datos diferentes.

### 4.3 Carrera de inicialización

- Las 3 réplicas ejecutan `DatabaseInitializer.Initialize()` al arrancar (`Program.cs:513-516`).
- En una base nueva pueden competir por crear tablas/triggers.
- El README de H2 ya advertía arrancar una sola réplica primero (`tests/h2/README.md:85-96`).

### 4.4 Riesgo Tailscale + Docker Desktop

Hay que validar que un contenedor de otra máquina pueda alcanzar la IP `100.x.x.x` del anfitrión. Si Docker Desktop/WSL2 no enruta a Tailscale, habrá que:

- usar una VPS,
- usar un túnel TCP,
- o ejecutar Tailscale dentro de Docker.

**Validar esto antes de dar por cerrada la arquitectura.**

---

## 5. Arquitectura objetivo

### PC anfitriona

Configuración real actual:

- Tailscale 1.104.2, nodo `taller-db`, IP `100.99.175.117`, DNS `taller-db.tail590083.ts.net`.
- Docker con MySQL 8 y Mailpit.
- MySQL:
  - `--log-bin-trust-function-creators=1`
  - `character-set-server=utf8mb4`
  - volumen `tallermecanico_taller-mysql-data`
  - `127.0.0.1:3307` para la app del anfitrión
  - `100.99.175.117:3307` para el equipo por Tailscale
- Mailpit:
  - SMTP `100.99.175.117:1026`
  - UI `http://100.99.175.117:8026`
  - también disponible en `127.0.0.1:1026`/`8026`
- Stack web del anfitrión:
  - 3 réplicas + Redis + Nginx
  - Nginx en `127.0.0.1:80`, `:443` y `:8088`
  - `tailscale serve --bg 8088` publica `https://taller-db.tail590083.ts.net`
- Backups manuales/programados con `mysqldump` (pendiente).
- PC sin suspensión automática (pendiente de configurar).

### Cada integrante

- Misma imagen canónica por digest.
- Sin MySQL local en el compose principal.
- `MYSQL_CONNECTION=Server=100.99.175.117;Port=3307;Database=taller_mecanico;User ID=taller;Password=<MYSQL_PASSWORD>;`
- Redis local para las 3 réplicas.
- `SMTP_HOST=100.99.175.117`, `SMTP_PORT=1026`, `SMTP_SECURITY=None` para Mailpit compartido.
- `APP_PUBLIC_BASE_URL=https://taller-db.tail590083.ts.net`; `APP_BIND_ADDRESS=127.0.0.1`.
- `APP_IMAGE=<imagen publicada>@sha256:<digest>`.
- Si usan Gmail en vez de Mailpit: `SMTP_HOST=smtp.gmail.com`, `SMTP_PORT=465`, `SMTP_SECURITY=SslOnConnect`, usuario Gmail y contraseña de aplicación.

### Sesiones entre integrantes

Compartir MySQL **no comparte cookies ni Data Protection**. Si el docente inicia sesión en la instancia de un integrante y salta a la de otro, probablemente deba volver a iniciar sesión. Si se requiere sesión compartida, habría que compartir también Data Protection y Redis desde el anfitrión.

---

## 6. Plan de implementación pendiente

### Fase 0 — Validar conectividad

```powershell
# Validado desde el anfitrión y desde un contenedor:
docker run --rm alpine sh -c "nc -vz 100.99.175.117 3307"
docker run --rm alpine sh -c "nc -vz 100.99.175.117 1026"
```

También validado: `host.docker.internal:3307` y `host.docker.internal:1026`. Docker Desktop sí enruta a la IP Tailscale en esta máquina.

### Fase 1 — Archivos de compose

Crear/modificar:

1. `docker-compose.yml` (principal, base remota):
   - Sin `mysql` local.
   - `image: ${APP_IMAGE:?Configura APP_IMAGE con el digest canónico}`
   - `pull_policy: always`
   - `ConnectionStrings__MySqlConnection: ${MYSQL_CONNECTION:?Configura MYSQL_CONNECTION}`
   - Mantener Redis local y las 3 réplicas.
   - `depends_on` solo de `redis`.

2. `docker-compose.host.yml` (solo PC anfitriona):
   - MySQL 8 con imagen fijada por digest.
   - Mailpit con imagen fijada por digest.
   - Puertos publicados en `${TAILSCALE_IP}:3306:3306`, `${TAILSCALE_IP}:1025:1025`, `${TAILSCALE_IP}:8025:8025`.
   - Volumen persistente.
   - `command: --log-bin-trust-function-creators=1 --character-set-server=utf8mb4`.
   - Healthchecks.

3. `docker-compose.local.yml` (fallback offline):
   - MySQL local + Mailpit local.
   - Cadena de conexión local.
   - Pensado para `docker compose -f docker-compose.yml -f docker-compose.local.yml ...` o equivalente.

4. `docker-compose.build.yml` (solo mantenedores):
   - `build: .`
   - Imagen local para publicar.
   - No usar en el flujo normal.

### Fase 2 — SMTP genérico

Modificar `Infraestructura/Correo/GmailEmailService.cs`:

- Quitar la restricción a `smtp.gmail.com`.
- Aceptar cualquier host no vacío.
- Validar puerto entre 1 y 65535.
- Agregar `Email:Smtp:Security`:
  - `None` (Mailpit)
  - `StartTls`
  - `SslOnConnect` (Gmail, valor por defecto)
- Usuario/contraseña opcionales:
  - ambos vacíos → no autenticar
  - uno vacío y el otro no → `ConfiguracionRegistroException`
- Mantener compatibilidad con Gmail.
- Mantener el manejo seguro de errores.

Actualizar pruebas:

- `tests/TallerMecanico.IntegrationTests/GmailEmailServiceTests.cs`
- Casos nuevos:
  - Mailpit sin autenticación
  - `Security=None`
  - host no Gmail válido
  - puerto no 465 válido
  - credenciales parciales inválidas

### Fase 3 — URL pública

`EnlaceVerificacion` ya lee `App:PublicBaseUrl`.

Opciones:

1. **Recomendada:** usar HTTPS con certificado de Tailscale:
   - `tailscale cert taller-db.tailnet.ts.net`
   - configurar nginx/instancia canónica.
   - `APP_PUBLIC_BASE_URL=https://taller-db.tailnet.ts.net`

2. Si se usará HTTP no loopback:
   - agregar una bandera explícita, por ejemplo `App:PermitirHttpNoSeguro=true`
   - permitir solo en Development
   - actualizar pruebas.

### Fase 4 — Inicialización segura

Modificar `Infraestructura/Persistencia/DatabaseInitializer.cs`:

- Adquirir lock de MySQL antes de crear/actualizar el esquema:

```sql
SELECT GET_LOCK(CONCAT('autotaller_schema:', DATABASE()), 30);
```

- Liberar con `RELEASE_LOCK` en `finally`.
- Si no se adquiere, lanzar error claro.

Modificar `Program.cs`:

- Reintentar la inicialización si la base remota no responde.
- Variables sugeridas:
  - `Database:IntentosInicio` (ej. 10)
  - `Database:EsperaEntreIntentosSegundos` (ej. 5)
- Loguear cada reintento sin exponer credenciales.

### Fase 5 — Variables de entorno

`.env.example` debe quedar con placeholders:

```env
APP_IMAGE=ghcr.io/arielflowers/tallermecanico@sha256:<digest>
MYSQL_CONNECTION=Server=100.x.x.x;Port=3306;Database=taller_mecanico;User ID=taller;Password=<password>;
SMTP_HOST=100.x.x.x
SMTP_PORT=1025
SMTP_SECURITY=None
SMTP_USERNAME=
SMTP_PASSWORD=
SMTP_FROM=AutoTaller <no-reply@autotaller.local>
APP_PUBLIC_BASE_URL=https://taller-db.tailnet.ts.net
TAILSCALE_IP=100.x.x.x
```

Para el anfitrión:

```env
MYSQL_ROOT_PASSWORD=<password>
MYSQL_PASSWORD=<password>
MYSQL_DATABASE=taller_mecanico
MYSQL_USER=taller
TAILSCALE_IP=100.x.x.x
```

No commitear `.env`.

### Fase 6 — Imagen canónica

1. Construir desde un commit acordado:

```powershell
docker build -t ghcr.io/arielflowers/tallermecanico:ec2-2026-10-23 .
```

2. Publicar:

```powershell
docker push ghcr.io/arielflowers/tallermecanico:ec2-2026-10-23
```

3. Obtener digest:

```powershell
docker image inspect ghcr.io/arielflowers/tallermecanico:ec2-2026-10-23 `
  --format '{{index .RepoDigests 0}}'
```

4. Fijar el digest en `APP_IMAGE`.

5. Fijar por digest MySQL, Mailpit, Redis y Nginx.

6. Limpiar `.dockerignore`:
   - `docs/`
   - `*.docx`
   - `Sprint_EC2_Plan.*`
   - `listado_archivos.txt`
   - archivos locales no necesarios

### Fase 7 — Migrar datos

En la base local canónica:

```powershell
mysqldump -h 127.0.0.1 -P 3307 -u taller -p `
  --single-transaction --triggers --routines `
  --no-tablespaces --set-gtid-purged=OFF `
  taller_mecanico > taller_mecanico_dump.sql
```

En el anfitrión:

1. Crear base y usuario.
2. Restaurar como `root` para evitar problemas con `DEFINER` del trigger.
3. Verificar `TRG_Servicios_HistorialCosto`.
4. Arrancar **una sola instancia** para aplicar migraciones pendientes.
5. Comparar conteos/checksum.

SQL de referencia:

```sql
CREATE DATABASE taller_mecanico CHARACTER SET utf8mb4;
CREATE USER 'taller'@'%' IDENTIFIED BY '<password-fuerte>';
GRANT SELECT, INSERT, UPDATE, DELETE, CREATE, ALTER, DROP, INDEX,
      REFERENCES, TRIGGER
ON taller_mecanico.* TO 'taller'@'%';
```

> No ejecutar los tests contra la base remota.

### Fase 8 — Verificación

Verificado el 23/10/2026 desde el anfitrión:

- Build Docker correcto.
- 70 pruebas post-merge correctas.
- `https://taller-db.tail590083.ts.net/Auth/Login` responde `200`.
- Registro real `prueba1792786760@example.com` creado.
- Correo visible en Mailpit con asunto `Verifica tu correo - AutoTaller Pro`.
- Enlace generado con `https://taller-db.tail590083.ts.net/Auth/VerificarEmail?...`.
- Confirmación del enlace responde `200` y muestra `verificado correctamente`.
- Trigger y 12 tablas presentes en la base.

Pendiente de verificar:

- Mismo digest de imagen en todas las máquinas.
- Dato creado en una máquina visible en otra.
- Registro desde una segunda máquina.
- Enlace de verificación desde una segunda máquina.
- Prueba con el anfitrión apagado: error claro y recuperable.
- Detener `app1`: las otras réplicas siguen.

```powershell
docker run --rm -v "${PWD}:/src" -w /src `
  -e TALLER_TEST_MYSQL="Server=host.docker.internal;Port=3307;User ID=root;Password=root_local;" `
  mcr.microsoft.com/dotnet/sdk:10.0 dotnet test `
  tests/TallerMecanico.IntegrationTests/TallerMecanico.IntegrationTests.csproj `
  --filter "FullyQualifiedName~GmailEmailServiceTests|FullyQualifiedName~RegistroFacadeTests|FullyQualifiedName~ReenvioVerificacionServiceTests|FullyQualifiedName~UsuarioAdministracionMySqlTests"
```

### Fase 9 — Documentación

Actualizar:

- `README.md`
- `docs/migraciones/README.md`
- `docs/PLAN_ENTORNO_REMOTO.md` (este archivo)
- `AGENTS.md` (aún no existe; crearlo si se desea)

---

## 7. Archivos que probablemente se modifiquen

- `docker-compose.yml`
- nuevos: `docker-compose.host.yml`, `docker-compose.local.yml`, `docker-compose.build.yml`
- `.env.example`
- `.dockerignore`
- `Infraestructura/Correo/GmailEmailService.cs`
- `Infraestructura/Persistencia/DatabaseInitializer.cs`
- `Program.cs`
- `tests/TallerMecanico.IntegrationTests/GmailEmailServiceTests.cs`
- `tests/TallerMecanico.IntegrationTests/RegistroFacadeTests.cs`
- `tests/TallerMecanico.IntegrationTests/ReenvioVerificacionServiceTests.cs`
- `README.md`
- `docs/migraciones/README.md`
- nuevos scripts en `scripts/` para dump, verificación y host

---

## 8. Riesgos y advertencias

- **La PC anfitriona debe estar encendida** durante pruebas y defensa.
- **Punto único de fallo**. Mitigación: backups, autoarranque, VPS de respaldo si es posible.
- **Todos comparten datos**: un integrante puede afectar a todos.
- **Tailscale debe estar activo** en todas las máquinas.
- **Docker Desktop puede no enrutar a la IP Tailscale**. Validar primero.
- **No exponer MySQL a Internet**.
- **No usar la base remota para tests**.
- **No commitear `.env` ni credenciales**.
- El trigger puede fallar al restaurar si se usa un usuario sin privilegios; restaurar como `root`.
- `DatabaseInitializer` necesita permisos DDL sobre la base remota.
- Si se usa HTTPS de Tailscale, no hace falta permitir HTTP no loopback.

---

## 9. Notas para el siguiente modelo

1. Leer primero:
   - `docker-compose.yml`, `docker-compose.host.yml`, `docker-compose.local.yml`
   - `.env.example` y `.env` local (no compartir)
   - `Infraestructura/Correo/GmailEmailService.cs`
   - `Aplicacion/Servicios/EnlaceVerificacion.cs`
   - `Infraestructura/Persistencia/DatabaseInitializer.cs`
   - `Program.cs` (bloque de inicialización)
2. Continuar en `feature/entorno-remoto-compartido`; no hacer push salvo que el usuario lo pida.
3. El anfitrión ya está operativo: `taller-db` / `100.99.175.117`.
4. Gmail ya está activo en el `.env` del anfitrión:
   - `SMTP_HOST=smtp.gmail.com`
   - `SMTP_PORT=465`
   - `SMTP_SECURITY=SslOnConnect`
   - `SMTP_USERNAME=apex.motorsBOL@gmail.com`
   - `SMTP_PASSWORD=<contraseña de aplicación de 16 caracteres, solo en .env>`
   - `SMTP_FROM=AutoTaller <apex.motorsBOL@gmail.com>`
5. La contraseña normal de la cuenta no sirve para SMTP; Gmail responde `535 5.7.8 BadCredentials`. Usar únicamente contraseña de aplicación y no guardarla en Git.
6. Si se publica la imagen, actualizar `APP_IMAGE` con el digest y ejecutar sin `docker-compose.build.yml`.
7. Si se migra el dump, restaurar como `root` por los `DEFINER` del trigger y respaldar antes.
8. Falta crear Administrador/Recepcionista; `--inicializar-usuarios` exige TTY interactiva.
