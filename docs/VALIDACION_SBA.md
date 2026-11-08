# Validación de la integración SBA

Fecha: 08/11/2026. Rama: `adrian/consolidar-4-apps`, sobre `main` con las cuatro
aplicaciones integradas.

## Compilación y pruebas automatizadas

La solución compiló sin errores ni advertencias. Las suites se ejecutaron con
MySQL 8.4 temporal; cada prueba crea su base `autotaller_test_*` y la elimina al
terminar. No se usó Aiven.

| Suite | Aprobadas | Fallidas | Omitidas |
|---|---:|---:|---:|
| Órdenes y catálogos | 420 | 0 | 6 |
| Frontend | 216 | 0 | 1 |
| Usuarios | 183 | 0 | 0 |
| Reportes | 43 | 0 | 2 |
| **Total** | **862** | **0** | **9** |

Las 9 omitidas son recorridos de navegador y lecturas de texto de PDF, que requieren
Playwright y Python con pypdf. En esta corrida local no estaban configurados; el CI
sí prepara esas herramientas.

Para reproducirlas, preparar las herramientas indicadas en [README](../README.md)
y ejecutar desde la raíz:

```console
dotnet build TallerMecanico.sln
dotnet test TallerMecanico.sln -m:1 -- xUnit.ParallelizeTestCollections=false
```

`-m:1` ejecuta los proyectos uno tras otro, porque varias pruebas web levantan las
cuatro aplicaciones como procesos.

## Docker y HTTPS

Se construyeron las cuatro imágenes y se levantó un proyecto Compose separado, con
certificados mkcert y una base descartable del MySQL de pruebas. Las cuatro apps
alcanzaron el estado `healthy` en el orden previsto. Desde el contenedor del Frontend:

| Comprobación | Resultado |
|---|---|
| HTTP plano hacia una API (`:8080`) | Rechazado |
| HTTPS sin confiar en la CA interna | Rechazado |
| HTTPS con la CA interna a las tres APIs | 200 |
| HTTPS con CA, sin clave interna | 401 |
| Llamar a una API desde la PC | Sin respuesta: no publica puertos |
| Login con credenciales falsas desde el Frontend | "Usuario o contraseña incorrectos.": llegó al Servicio 2 por HTTPS |

`HttpsInternoTests` comprueba además que el Frontend rechaza otra CA y un nombre de
servicio distinto. `docker compose config` es válido y lista exactamente
`frontend`, `servicio-ordenes`, `servicio-usuarios` y `servicio-reportes`.

## Lanzadores

`tests/entorno-portable.sh` pasó con Docker y HTTP simulados, incluido un clon con
espacios en su ruta. Comprueba construcción, arranque, espera de salud, estado,
logs, inicialización de cuentas en Usuarios, apagado, propagación de errores y
rechazo de claves internas inválidas, sin imprimir secretos.

## Evidencia visual

Capturas generadas con cuentas y datos ficticios, sin la configuración privada de
Aiven o SMTP:

| Evidencia | Archivo |
|---|---|
| Confirmación de operación sobre un usuario | [Confirmación](evidencias/sba/confirmacion-usuario.png) |
| Formulario de invitación y validaciones | [Crear usuario](evidencias/sba/crear-usuario.png) |
| Cambio de contraseña | [Contraseña](evidencias/sba/cambiar-contrasena.png) |
| Reporte de órdenes y estados | [Órdenes](evidencias/sba/estados-ordenes.png) |
| Filtros y gráfico del reporte por mecánico | [Mecánicos](evidencias/sba/filtro-mecanicos.png) |

## Prueba manual del equipo

Con el despliegue real (`dotnet msbuild -t:NubeUp`), abrir http://127.0.0.1:8088 o
https://127.0.0.1 y recorrer:

1. Login como Administrador y Recepcionista; comparar menús y accesos.
2. Crear una orden saliendo a registrar cliente, vehículo y producto; comprobar que
   el borrador se conserva y que el comprobante se abre solo.
3. Intentar más stock del disponible.
4. Anular la orden; comprobar que el stock vuelve y que aparece en la bitácora.
5. Invitar una cuenta ficticia identificable y confirmar que llega el correo.
6. Activarla, comprobar que el enlace no se reutiliza y cambiar la contraseña.
7. Editar, desactivar y reactivar; comprobar sesión y bitácora.
8. Filtrar y exportar los dos reportes; abrir PDF y Excel.

La recepción real del correo, la entrega al LMS y la defensa no se sustituyen con las
pruebas automatizadas: las de correo usan SMTP simulado.
