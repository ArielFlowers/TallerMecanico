# Administración de la base Aiven

La aplicación utiliza únicamente la base compartida `taller_mecanico` de Aiven
MySQL 8.4. Los compañeros siguen [README.md](../README.md) y usan `NubeUp`.
Esta guía corresponde al responsable de respaldos, recuperación y diagnóstico.

## Conexiones privadas

Cada PC usa `MYSQL_CONNECTION` de `.env`, con el host, puerto, usuario y clave del
mismo servicio Aiven, `Database=taller_mecanico`, `SslMode=Required` y
`Maximum Pool Size=3`. El resto de las siete variables visibles configura SMTP.
Las cuentas web se conservan y son distintas de las credenciales SQL o SMTP.

El responsable completa `bd-nube.env` a partir de `bd-nube.env.example`. Ese
archivo contiene `NUBE_HOST`, `NUBE_PUERTO`, `NUBE_USUARIO`, `NUBE_BD` y
`MYSQL_PWD`, sin comillas ni espacios alrededor de `=`. Los scripts lo entregan
al cliente MySQL de Docker; no extraen credenciales de `.env`. No sobrescribir
archivos privados existentes ni publicar su contenido.

La cadena local de ejemplo de appsettings se retiró. Los entornos de prueba
inyectan sus conexiones aisladas; el Compose diario inyecta la de Aiven.

## Diagnosticar el servicio

Confirmar en la consola de Aiven que se usa el servicio del equipo y está
activo. Consultar versión, tablas, trigger, conexiones y conteos:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/bd/verificar-nube.ps1
```

Después del arranque se esperan las doce tablas de la aplicación y
`TRG_Servicios_HistorialCosto`. El esquema se inicializa desde la aplicación,
coordinado mediante un bloqueo MySQL entre réplicas; no se carga una seed.

Mantener `Maximum Pool Size=3` por réplica. Antes de aumentarlo, consultar el
límite y uso efectivos del servicio, considerando todas las PCs y administración.
Los límites y versiones documentados en [VALIDACION_MYSQL84.md](VALIDACION_MYSQL84.md)
son evidencia fechada, no una garantía de valores futuros.

La prueba administrativa `probar-trigger-nube.ps1` crea y elimina una base
auxiliar. Solo corresponde a una preparación o diagnóstico coordinado por el
responsable; no forma parte del arranque diario ni de la validación de esta
simplificación.

## Respaldar y restaurar

Crear un respaldo antes de cambios de datos importantes:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/bd/respaldar-nube.ps1
```

El dump queda privado en `respaldos/`, con fecha, segundos y milisegundos. Validar
que existe, no está vacío y corresponde al destino esperado. Contiene datos y
cuentas. Los scripts conservan UTF-8 mediante archivos y montajes; no pasar SQL
por `>` u `Out-File` de PowerShell 5.1.

Para restaurar, coordinar que el equipo deje de escribir, respaldar el estado
actual y revisar el dump y el destino. **La importación reemplaza las tablas que
contiene**, por lo que exige confirmación explícita:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/bd/importar-dump-nube.ps1 -Archivo respaldos/<dump-validado>.sql -Confirmar
```

Los dumps omiten triggers para evitar problemas con `DEFINER`. Después de una
restauración, arrancar la aplicación para recrearlos y comprobar conteos y
relaciones. Para revisar primero una réplica:

```powershell
docker compose -f docker-compose.yml build app1
docker compose -f docker-compose.yml up -d --no-build redis app1
docker compose -f docker-compose.yml logs app1 --tail 80
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/bd/verificar-nube.ps1
```

Después levantar las tres réplicas y Nginx con `dotnet msbuild -t:NubeUp`.
Los compañeros no repiten importaciones al instalar el proyecto.

## Cuentas de acceso

Conservar las cuentas existentes. Para crear un administrador y un recepcionista
con nombres nuevos, una persona ejecuta desde su terminal interactiva:

```powershell
.\taller nube usuarios
```

El comando solicita confirmación, nombres y contraseñas. Cada cuenta se guarda
al crearla; si falla la segunda, la primera puede haber quedado creada. No
repetir con nombres existentes.

La consola existente permite restablecer únicamente la cuenta activa llamada
exactamente `admin`, con rol Administrador, en Development:

```powershell
docker compose -f docker-compose.yml exec app1 dotnet TallerMecanico.dll --restablecer-admin
```

Pide `CONFIRMAR` y una contraseña de 12 a 128 caracteres, que no se muestra.
Si el administrador tiene otro nombre, ese comando no lo restablece.

## Recursos locales antiguos

El modo local y el anfitrión/Tailscale se retiraron. Sus contenedores, volúmenes
y respaldos privados se conservan detenidos; no se fusionan sus datos con Aiven.
La seed de demostración y sus comandos también se retiraron.

Si una PC todavía tiene activo el proyecto anterior, revisar sus contenedores:

```powershell
docker ps -a --filter label=com.docker.compose.project=tallermecanico-local-84
```

Después de construir correctamente la imagen con `NubeBuild`, detener mediante
`docker stop` únicamente los contenedores confirmados de ese proyecto y ejecutar
`NubeUp`. No borrar recursos para liberar puertos. Esta transición se hace una
vez; el lanzador ya no alterna entre entornos. El MySQL original de la migración
permanece protegido por los filtros de proyecto y servicio de `NubeDown`.

Si hay que recuperar datos de un contenedor conservado, el exportador histórico
continúa disponible para el responsable:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/bd/exportar-dump-local.ps1 -Contenedor <contenedor-origen-confirmado> -BaseDatos taller_mecanico
```

Arrancar ese MySQL solo para recuperar su copia y detenerlo después. No montar
un volumen antiguo directamente en una versión distinta de MySQL. La importación
a Aiven sigue requiriendo la revisión y confirmación de la sección anterior.

## Pruebas y problemas frecuentes

Las pruebas automáticas mantienen MySQL 8.4 temporal: ver
[tests/README.md](../tests/README.md). Nunca apuntar `TALLER_TEST_MYSQL` a Aiven.
Los scripts administrativos pueden usar la imagen cliente `mysql:8.4` sin
levantar otro servidor de trabajo.

| Síntoma | Acción |
|---|---|
| Reintentos SQL o HTTP 502 | Revisar que Aiven está activo, host/puerto, Internet y logs. Repetir NubeUp después de corregir la causa. |
| Access denied | Revisar privadamente las credenciales y el formato del archivo correspondiente. |
| Error de trigger o privilegios | Revisar cuenta y permisos con el responsable; no continuar una restauración con el trigger fallido. |
| Pool agotado | Consultar uso y límite del servidor antes de aumentar conexiones. |
| Puerto ocupado | Identificar el proceso o contenedor y detener solo el recurso acordado. |
| Certificados no encontrados | Revisar los dos PEM en nginx/certs y seguir la guía mkcert de README. |

Mantener `.env`, `bd-nube.env`, `respaldos/` y claves de certificados fuera de Git
y del contexto de build. Las validaciones anteriores conservan sus fechas;
[VALIDACION_NUBE_UNICA.md](VALIDACION_NUBE_UNICA.md) registra esta transición.
