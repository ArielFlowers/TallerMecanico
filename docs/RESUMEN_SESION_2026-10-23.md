# Resumen de sesión: entorno remoto compartido

> Fecha: 23/10/2026  
> Rama: `feature/entorno-remoto-compartido`  
> Commit: `c7a4297` (`feat(infra): habilitar entorno remoto compartido con Tailscale y SMTP configurable`)  
> Estado: anfitrión operativo, Gmail activo y flujo de registro/verificación probado de punta a punta.

## 1. Resultado final

El sistema ya puede ejecutarse contra una base MySQL única alojada en la PC del anfitrión y publicarse por Tailscale. El correo de verificación sale por Gmail y el enlace HTTPS funciona.

Validado:

- 70/70 pruebas de integración afectadas.
- Build de la imagen Docker con .NET 10.
- Los 4 compose pasan `docker compose config`.
- Conectividad Docker → Tailscale en MySQL y Mailpit.
- Registro con Mailpit: correo, enlace y verificación.
- Registro con Gmail real: correo, enlace y verificación.
- App respondiendo `200` en `https://taller-db.tail590083.ts.net`.

## 2. Datos del entorno

| Recurso | Valor |
|---|---|
| Nodo Tailscale | `taller-db` |
| IP Tailscale | `100.99.175.117` |
| DNS/URL HTTPS | `https://taller-db.tail590083.ts.net` |
| Tailnet | `tail590083.ts.net` |
| MySQL | `100.99.175.117:3307` |
| Base de datos | `taller_mecanico` |
| Usuario MySQL | `taller` |
| Mailpit SMTP | `100.99.175.117:1026` |
| Mailpit UI | `http://100.99.175.117:8026` |
| Cuenta Gmail remitente | `apex.motorsBOL@gmail.com` |
| Stack web del anfitrión | Redis + 3 réplicas + Nginx |
| Nginx local del anfitrión | `127.0.0.1:80`, `:443`, `:8088` |

Las contraseñas reales de MySQL y la contraseña de aplicación de Gmail están solo en el `.env` del anfitrión. No están en Git ni en este documento.

## 3. Trabajo realizado

### Código

- `GmailEmailService` ahora es un cliente SMTP genérico:
  - host y puerto configurables;
  - `Email:Smtp:Security` con `None`, `StartTls` o `SslOnConnect`;
  - autenticación opcional;
  - validación de credenciales parciales.
- `EnlaceVerificacion` permite HTTP no loopback solo con `App:PermitirHttpNoSeguro=true`.
- `DatabaseInitializer` usa `GET_LOCK`/`RELEASE_LOCK` con nombre derivado de la base para evitar carreras entre réplicas.
- `Program.cs` reintenta la inicialización de MySQL con `Database:IntentosInicio` y `Database:EsperaEntreIntentosSegundos`.
- `nginx/nginx.conf` conserva `X-Forwarded-Proto` para que Tailscale Serve no convierta los enlaces HTTPS en HTTP.

### Docker e infraestructura

- `docker-compose.yml`: stack remoto sin MySQL local, con Redis, 3 réplicas y Nginx.
- `docker-compose.host.yml`: MySQL 8 + Mailpit solo en el anfitrión.
- `docker-compose.local.yml`: stack completo offline con MySQL y Mailpit locales.
- `docker-compose.build.yml`: build local de la imagen canónica.
- `Dockerfile`: imágenes base parametrizables por digest.
- `.dockerignore`: excluye documentación, secretos y artefactos locales.
- `.env.example`: variables del entorno remoto, puertos del anfitrión y URL pública.
- `APP_BIND_ADDRESS`: permite publicar Nginx en la IP de Tailscale si se necesita.

### Documentación

- `README.md` actualizado con el anfitrión actual y comandos.
- `docs/PLAN_ENTORNO_REMOTO.md` con plan, diagnóstico, decisiones y estado.
- `docs/migraciones/README.md` y `tests/README.md` ajustados.

### Anfitrión

- Tailscale 1.104.2 instalado y autenticado.
- Nodo renombrado a `taller-db`.
- MySQL 8 y Mailpit levantados con `docker-compose.host.yml`.
- Base `taller_mecanico` inicializada por la app.
- Esquema con 12 tablas y trigger `TRG_Servicios_HistorialCosto`.
- 3 réplicas + Redis + Nginx levantados.
- `tailscale serve --bg 8088` publica HTTPS hacia `127.0.0.1:8088`.
- Gmail configurado con contraseña de aplicación.
- Se reinició Docker Desktop por una falla temporal; los servicios quedaron nuevamente arriba.

## 4. Validaciones realizadas

- `docker build -t taller-mecanico:local-test .` correcto.
- 70 pruebas post-merge correctas:
  - `GmailEmailServiceTests`: 21.
  - `RegistroFacadeTests` + `ReenvioVerificacionServiceTests`: 19.
  - `UsuarioAdministracionMySqlTests`: 30.
- `docker compose config` válido para principal, host, local y build.
- `nc -vz` correcto a `100.99.175.117:3307` y `:1026` desde un contenedor.
- `https://taller-db.tail590083.ts.net/Auth/Login` responde `200`.
- Registro real con Mailpit y con Gmail.
- Enlace de verificación confirmado con `200` y mensaje `verificado correctamente`.
- Solicitud de prueba eliminada de MySQL; la base quedó sin solicitudes.

## 5. Instrucciones para los compañeros

### Requisitos

1. Instalar Tailscale.
2. Pedir al anfitrión una invitación a la tailnet `tail590083.ts.net`.
3. Verificar que el nodo aparece:

```powershell
tailscale status
tailscale ping taller-db
```

4. Probar que Docker puede llegar a la base:

```powershell
docker run --rm alpine sh -c "nc -vz 100.99.175.117 3307; nc -vz 100.99.175.117 1026"
```

Si falla, revisar Tailscale y Docker Desktop antes de continuar.

### Opción A: usar directamente el anfitrión

Con Tailscale activo, abrir:

```text
https://taller-db.tail590083.ts.net
```

No necesitan Docker ni `.env`. Esta es la forma recomendada para probar el sistema compartido y para la demostración.

### Opción B: correr sus propias 3 réplicas

Actualizar el repo y cambiar a la rama:

```powershell
git fetch origin
git switch feature/entorno-remoto-compartido
```

Crear `.env` desde `.env.example` y completar:

```dotenv
APP_IMAGE=taller-mecanico:local

MYSQL_CONNECTION=Server=100.99.175.117;Port=3307;Database=taller_mecanico;User ID=taller;Password=<MYSQL_PASSWORD>;
MYSQL_PASSWORD=<MYSQL_PASSWORD>

SMTP_HOST=100.99.175.117
SMTP_PORT=1026
SMTP_SECURITY=None
SMTP_USERNAME=
SMTP_PASSWORD=
SMTP_FROM=AutoTaller <no-reply@autotaller.local>

APP_BIND_ADDRESS=127.0.0.1
APP_PUBLIC_BASE_URL=https://taller-db.tail590083.ts.net
APP_PERMITIR_HTTP_NO_SEGURO=false
```

Pedir `MYSQL_PASSWORD` al anfitrión por un canal privado. No commitear `.env`.

Levantar:

```powershell
docker compose -f docker-compose.yml -f docker-compose.build.yml up -d --build
```

Abrir la instancia local:

```text
http://127.0.0.1:8088
```

Los enlaces de verificación apuntan a la URL del anfitrión y funcionan porque la base es compartida.

### Reglas

- No ejecutar `docker-compose.host.yml` fuera de la PC anfitriona.
- No usar `docker-compose.local.yml` si se quieren datos compartidos; ese compose usa base local.
- No apuntar las pruebas automáticas a la base remota; usar `TALLER_TEST_MYSQL` local.
- No commitear `.env` ni `nginx/certs/`.
- Todos comparten datos: un cambio afecta a todo el equipo.
- La PC anfitriona debe quedar encendida y sin suspensión.

## 6. Correo

Gmail está activo en el anfitrión. El correo de verificación se envía a la dirección escrita en el formulario de registro; no todo llega a la cuenta `apex.motorsBOL@gmail.com`.

- Si se registra `apex.motorsBOL+prueba@gmail.com`, el correo llega a esa misma bandeja.
- Mailpit sigue disponible como respaldo para las instancias locales que no usen Gmail.
- Si un compañero corre su propia instancia y quiere Gmail, debe configurar su propio remitente o usar la URL del anfitrión. La contraseña de aplicación no debe compartirse en el repositorio.

## 7. Pendientes antes de la entrega

1. Crear el Administrador y el Recepcionista:

```powershell
docker exec -it tallermecanico-app1-1 dotnet TallerMecanico.dll --inicializar-usuarios
```

2. Importar el dump de la base más completa. La base actual está recién inicializada.
3. Publicar la imagen en GHCR o Docker Hub y fijar `APP_IMAGE` por digest.
4. Probar el acceso desde una segunda PC con Tailscale.
5. Configurar respaldo de MySQL y evitar suspensión automática.
6. Evaluar la creación de `AGENTS.md`.

## 8. Comandos rápidos del anfitrión

```powershell
# Servicios compartidos
docker compose -f docker-compose.host.yml up -d

# Aplicación + Redis + Nginx
docker compose -f docker-compose.yml -f docker-compose.build.yml up -d --build

# Estado
docker compose -f docker-compose.yml -f docker-compose.build.yml ps

# HTTPS
tailscale serve --bg 8088
tailscale serve status

# Logs de una réplica
docker logs tallermecanico-app1-1 --tail 50
```

## 9. Notas y riesgos

- Docker Desktop puede fallar al reiniciarse; si ocurre, iniciar Docker Desktop y esperar a que `docker info` responda.
- Si `origin/main` avanza, hacer `git fetch origin` y `git merge --ff-only origin/main`, luego volver a correr las pruebas.
- El trigger se debe restaurar como `root` si se importa un dump con `DEFINER`.
- No guardar contraseñas en documentación, commits ni capturas.
- La rama ya fue publicada en `origin/feature/entorno-remoto-compartido`.
