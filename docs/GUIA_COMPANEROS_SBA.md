# Guía para el equipo: integración de Usuarios, Reportes y SBA

Rama de trabajo: `alex/usuarios-reportes-sba`. Destino del PR: `G2-main`.

## Qué se completó

El producto ahora ejecuta cuatro aplicaciones:

```text
Navegador → Frontend Razor
                ├─ HTTP → servicio-ordenes  → MySQL compartido
                ├─ HTTP → servicio-usuarios → MySQL compartido
                └─ HTTP → servicio-reportes → MySQL compartido
```

| Área | Cambio |
|---|---|
| Usuarios, backend | Se trasladaron dominio, puertos, servicios, repositorios, correo y Argon2id. Se implementaron las APIs de autenticación y administración. |
| Usuarios, frontend | Login, invitación, edición, baja, reactivación y contraseña consumen `UsuariosApi` de forma asíncrona. La cookie y la sesión siguen en el Frontend. |
| Sesiones y permisos | El Frontend consulta el estado de la cuenta por API; una baja o cambio de rol revoca la sesión. El Recepcionista puede consultar su estado y no puede administrar usuarios. |
| Reportes | Se conserva la implementación del equipo y se integra su despliegue independiente. Las páginas consumen su API. |
| Órdenes e inicio | Se completó la conexión HTTP con los casos de uso y el dashboard existentes. Los buscadores conservan el catálogo y las opciones que tenían. |
| Limpieza del Frontend | Se retiraron copias de lógica, entidades y persistencia que ya viven en los servicios. Por eso el diff contiene eliminaciones y movimientos; las funcionalidades siguen en el backend. |
| Docker | Un Dockerfile por aplicación, healthchecks y arranque ordenado. Se retiraron réplicas, Nginx y Redis. HTTPS lo sirve Kestrel. |
| Lanzadores | Windows y macOS construyen y levantan las cuatro aplicaciones; la creación de cuentas por consola se ejecuta en Usuarios. |
| Pruebas y CI | Se trasladaron las pruebas de Usuarios y se adaptaron las de HTTP. CI ejecuta las cuatro suites desde la solución. |

Las pantallas, los estilos y los scripts de la aplicación conservan su diseño y
sus flujos. Los cambios de vistas de Órdenes corresponden a tipos DTO.
No se reescribieron las reglas transaccionales de Órdenes ni los generadores de
Reportes. No se agregó una migración de esquema para esta integración.

## Cómo revisar la rama antes de integrar

Desde un clon con los cambios propios guardados:

```console
git fetch origin
git switch --track origin/alex/usuarios-reportes-sba
```

Si la rama ya existe localmente, usar `git switch alex/usuarios-reportes-sba`.
Después del merge del PR, continuar desde `G2-main` y actualizarla con
`git pull --ff-only`.

## Qué debe preparar cada compañero

1. Conservar su `.env` privado con la conexión Aiven y los datos SMTP. La aplicación
   usa `.env`; `bd-nube.env` es un respaldo anterior.
2. Agregar una `CLAVE_INTERNA` aleatoria de al menos 32 caracteres. No es la clave
   de MySQL ni la de Gmail. Los comandos para generarla están en
   [README, preparación](../README.md#preparar-windows-o-macos).
3. Tener `certs/localhost.pem` y `certs/localhost-key.pem`, generados y confiados
   con mkcert en su propia PC. Si ya tiene los certificados anteriores de
   `nginx/certs/`, puede copiarlos a `certs/`.
4. Iniciar Docker Desktop con contenedores Linux. No se necesita Redis, Nginx ni
   MySQL local para ejecutar la aplicación conectada a Aiven.

Cada compañero genera su propia clave interna y usa ese mismo valor para las
cuatro aplicaciones de su equipo; Compose realiza esa distribución. No hay que
compartir la clave interna entre PCs. `.env` y los certificados están ignorados
por Git y excluidos de las imágenes.

## Cómo levantar y verificar

Desde la raíz del repositorio, en Windows o macOS:

```console
dotnet msbuild -t:NubeUp
dotnet msbuild -t:NubeStatus
```

El estado debe mostrar `frontend`, `servicio-ordenes`, `servicio-usuarios` y
`servicio-reportes`, todos saludables. Abrir http://127.0.0.1:8088 o
https://127.0.0.1. Solo el Frontend publica puertos al host; las APIs quedan en
la red de Docker.

| Acción | Comando |
|---|---|
| Ver logs | `dotnet msbuild -t:NubeLogs` |
| Apagar conservando volúmenes | `dotnet msbuild -t:NubeDown` |
| Reconstruir imágenes | `dotnet msbuild -t:NubeBuild` |
| Crear cuentas, Windows interactivo | `.\taller nube usuarios` |
| Crear cuentas, macOS interactivo | `bash ./taller.sh nube usuarios` |

El primer `NubeUp` retira contenedores anteriores mediante `--remove-orphans`,
sin eliminar sus volúmenes. El esquema compartido lo inicializa solo Órdenes.
La sesión y los borradores están en memoria del Frontend: reiniciarlo descarta
los borradores en curso. Las claves de las cookies conservan su volumen.

## Recorrido manual para el grupo 2

1. Ingresar como Administrador y Recepcionista; comprobar menú y accesos.
2. Invitar una cuenta ficticia identificable y confirmar que llegue el correo.
3. Activarla y comprobar que el enlace no funcione una segunda vez.
4. Editar, desactivar y reactivar; comprobar la revocación de sesión y la bitácora.
5. Cambiar la contraseña; comprobar cierre de sesión y nuevo login.
6. Filtrar los reportes de Órdenes y Mecánicos, exportar ambos PDF y Excel y
   abrirlos con los lectores que se usarán en la defensa.

Las pruebas automatizadas usan SMTP simulado. La recepción del correo en un
buzón real debe confirmarse en este recorrido.

## Qué ya se verificó

| Suite | Aprobadas | Fallidas | Omitidas |
|---|---:|---:|---:|
| Usuarios | 180 | 0 | 0 |
| Reportes | 43 | 0 | 0 |
| Órdenes y catálogos | 350 | 0 | 0 |
| Frontend, HTTP e interfaz | 401 | 0 | 0 |
| **Total** | **974** | **0** | **0** |

Además: compilación sin errores ni advertencias, 19 comprobaciones Docker,
HTTPS verificado, exportaciones en Linux y lanzadores con rutas que contienen
espacios. Se conservaron cinco capturas de la UI.

Las pruebas de persistencia y navegador se ejecutaron con bases temporales;
nunca se debe configurar `TALLER_TEST_MYSQL` con Aiven. CI quedó actualizado;
su resultado remoto y la compatibilidad nativa de Windows se revisan en los
checks del PR antes de integrar.

## Dónde encontrar el detalle y qué revisar en el PR

- [Validación y resultados](VALIDACION_SBA.md): alcance, pruebas y despliegue.
- [Material para la entrega](ENTREGA_SBA_USUARIOS_REPORTES.md): C4, clases,
  patrones, SOLID y capturas para el documento grupal.
- [Arquitectura SBA](SBA.md) y [Hexagonal](ARQUITECTURA_HEXAGONAL.md).
- [Matriz de la rúbrica](CUMPLIMIENTO_RUBRICA.md).
- [CI](CI.md): filtros, herramientas y resultados por suite.

La revisión debe concentrarse en los contratos y roles de Usuarios, la frontera
HTTP del Frontend, los cambios de despliegue y la conservación de los recorridos
de Órdenes y Reportes. El PR se integra en `G2-main` después de la revisión del
grupo y de comprobar sus checks.
