# Validación de la integración SBA

Fecha: 06/11/2026. Rama de trabajo: `alex/usuarios-reportes-sba`.

## Alcance

Se completó la extracción de Usuarios, su consumo HTTP desde Razor y el
despliegue de las cuatro aplicaciones. Reportes conserva la implementación del
equipo. Órdenes y el dashboard reutilizan los casos de uso existentes mediante
sus contratos HTTP; se retiró su código duplicado del Frontend.

Los formularios, las rutas, los estilos y los scripts de la aplicación conservan
su comportamiento. En las vistas de Órdenes solo se actualizaron las referencias
a los DTO. Los scripts de pruebas admiten `PLAYWRIGHT_CHANNEL=chromium`, manteniendo
Edge como valor predeterminado.

## Compilación y pruebas automatizadas

La solución y los proyectos de pruebas compilaron sin errores ni advertencias.
Las suites se ejecutaron con MySQL 8.4 temporal, Python con pypdf y Chromium de
Playwright. Las pruebas crearon bases `autotaller_test_*` y las eliminaron al
terminar. No se usó Aiven para probar.

| Suite | Aprobadas | Fallidas | Omitidas |
|---|---:|---:|---:|
| Usuarios | 180 | 0 | 0 |
| Reportes | 43 | 0 | 0 |
| Órdenes y catálogos | 350 | 0 | 0 |
| Frontend, HTTP e interfaz | 401 | 0 | 0 |
| **Total** | **974** | **0** | **0** |

El [resumen en JSON](evidencias/sba/resultados.json) se extrajo de los contadores
TRX e incorpora los resultados de Docker y del despliegue, sin credenciales.

Se verificaron, entre otros, login por rol, invitación y activación, restricciones
de edición y baja, revocación de sesión por cambio de cuenta, contraseña,
auditoría, filtros, borradores, tokens, transacciones y exportaciones. Las pruebas
de correo utilizan SMTP simulado; no acreditan recepción en Gmail.

Para reproducirlas, preparar las herramientas indicadas en [README](../README.md)
y ejecutar desde la raíz:

```console
dotnet build TallerMecanico.sln
dotnet test TallerMecanico.sln -- xUnit.ParallelizeTestCollections=false
```

## Docker y HTTPS

Se construyeron las cuatro imágenes y se levantó un proyecto de prueba separado,
con una base descartable y puertos locales 18080, 18088 y 18443. Las cuatro apps
alcanzaron el estado `healthy` en el orden previsto.

Pasaron **19 comprobaciones**:

- HTTP del Frontend, login público y salud de las cuatro aplicaciones.
- HTTPS servido por Kestrel, verificando el certificado contra la CA de mkcert.
- APIs sin puertos publicados hacia la PC.
- Frontend sin cadena MySQL ni credenciales SMTP en su entorno.
- Rechazo 401 de una llamada sin clave interna.
- Consulta del estado de cuenta con clave, sin exigir rol Administrador.
- Rechazo 403 de administración de usuarios como Recepcionista.
- Consulta de usuarios como Administrador.
- Ambos reportes en PDF y Excel dentro de los contenedores Linux, comprobando
  cabecera PDF y estructura ZIP/XLSX de los archivos devueltos.

El proyecto de prueba reutilizó el Compose del repositorio con configuración
aislada. No se sustituyó el despliegue conectado a la base compartida para
realizar estas comprobaciones.

Después de finalizar y retirar el entorno temporal se ejecutó el target real
`NubeUp`. Quedaron saludables `frontend`, `servicio-ordenes`,
`servicio-usuarios` y `servicio-reportes`. El login HTTP en 8088 y la salud HTTPS
en 443 respondieron 200. Se retiraron los contenedores anteriores de réplicas,
Nginx, Redis y MySQL local; el volumen histórico
`tallermecanico_taller-mysql-data` se conservó y se verificó su existencia.
Este arranque no creó cuentas ni datos ficticios en Aiven.

## Lanzadores y CI

`tests/entorno-portable.sh` pasó con Docker y HTTP simulados, incluido un clon
con espacios en su ruta. Comprueba construcción, arranque, espera de salud,
estado, logs, inicialización de cuentas en Usuarios, apagado, propagación de
errores y rechazo de claves internas inválidas, sin imprimir secretos.

El workflow mantiene sus áreas y ahora compila y prueba `TallerMecanico.sln`,
incluidas las cuatro suites. Usa nombres TRX generados para conservar cada
resultado. Los jobs de compatibilidad publican cada aplicación por separado;
Windows también valida la sintaxis del lanzador PowerShell.

Se comprobaron localmente el YAML, la sintaxis Bash y el lanzador simulado.
La ejecución remota de GitHub Actions y el recorrido nativo de Windows deben
comprobarse al subir el PR; no se ejecutaron desde esta revisión en macOS.

Las cinco capturas de [Usuarios y Reportes](ENTREGA_SBA_USUARIOS_REPORTES.md)
se guardaron en un recorrido adicional aprobado sobre otra base descartable.

## Prueba manual del equipo

El comando habitual prepara el despliegue real con la configuración privada de
cada equipo:

```console
dotnet msbuild -t:NubeUp
```

Abrir http://127.0.0.1:8088 o https://127.0.0.1 y recorrer:

1. Login como Administrador y Recepcionista; comparar menús y accesos.
2. Invitar una cuenta ficticia identificable y confirmar recepción del correo.
3. Activarla, comprobar que el enlace no se reutiliza y cambiar la contraseña.
4. Editar, desactivar y reactivar; comprobar sesión y auditoría.
5. Filtrar y exportar los dos reportes; abrir PDF y Excel.

La recepción real del correo, la entrega al LMS y la defensa del equipo no se
sustituyen con las pruebas automatizadas. HTTPS interno entre Frontend y APIs
permanece como mejora opcional, tal como establece el plan.
