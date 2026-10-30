# Pruebas de AutoTaller Pro

Última ejecución completa, 30/10/2026: **514 aprobadas, 0 fallidas y 1 omitida**
(envío real manual de Gmail), de 515 casos. El detalle de las correcciones y su
evidencia está en [VALIDACION_PRUEBAS.md](../docs/VALIDACION_PRUEBAS.md).

Aiven es la única base de trabajo de la aplicación. El MySQL descrito aquí es
exclusivamente temporal para tests; no es un modo local de uso diario ni usa
la seed de demostración retirada. Se conservan también las pruebas aisladas H2.

La suite usa xUnit y MySQL 8.4. Todas las pruebas de persistencia, incluidas las de autenticación y repositorios, crean una base temporal `autotaller_test_<guid>` y la eliminan al finalizar; no usan ni modifican la base del taller. El usuario configurado debe poder crear y eliminar bases en el servidor de pruebas. La conexión de estas pruebas no se obtiene de User Secrets y no se requieren cuentas del taller. La fixture rechaza servidores remotos, incluido Aiven.

La configuración de pruebas está en esta carpeta: `tests/.env.test.example`
y `tests/docker-compose.test.yml`. Si se conserva un `.env.test` privado en la
raíz de un clon anterior, moverlo primero a `tests/.env.test` sin sobrescribir
uno existente. Este archivo está ignorado por Git.

Desde la **raíz del repositorio**:

```powershell
if (-not (Test-Path -LiteralPath tests/.env.test)) { Copy-Item tests/.env.test.example tests/.env.test }
notepad tests/.env.test
docker compose --project-directory . --env-file tests/.env.test -f tests/docker-compose.test.yml up -d
```

Completar las contraseñas antes de iniciar el contenedor.

`--project-directory .` conserva el nombre de proyecto y la red del comando
anterior aunque el Compose esté ahora dentro de `tests/`. El contenedor mantiene
el puerto `127.0.0.1:3308` y su almacenamiento temporal. Configurar
`TALLER_TEST_MYSQL` con ese host, puerto y la clave root de pruebas, sin imprimirla
ni guardarla en Git. No apuntarla a Aiven ni a las bases de trabajo del equipo.

## Suite completa en Windows

1. Preparar el MySQL exclusivo de pruebas del párrafo anterior y configurar
   `TALLER_TEST_MYSQL` con su conexión local (puerto 3308). La fixture admite
   loopback y los nombres de servicio Docker documentados; nunca un servidor remoto.
2. Preparar Playwright como indica la sección Navegador y configurar
   `PLAYWRIGHT_MODULE`. Deben estar instalados Node.js y Microsoft Edge.
3. Configurar `TALLER_PDF_PYTHON` con Python que tenga `pypdf` para incluir
   todas las verificaciones de contenido PDF.
4. Ejecutar desde la raíz:

   ```powershell
   dotnet test tests/TallerMecanico.IntegrationTests/TallerMecanico.IntegrationTests.csproj --logger "trx;LogFileName=integracion.trx" --results-directory TestResults -- xUnit.ParallelizeTestCollections=false
   ```

Los recorridos de Edge son headless y crean aplicaciones HTTPS y bases descartables.
Comprueban el catálogo, varios servicios, cantidades, devoluciones entre CRUD,
confirmación/cancelación y comprobantes en modal. No requieren el certificado de
Nginx ni cambian la confianza de Windows.

Sin MySQL, Playwright o la herramienta PDF, los casos que los requieren se marcan
como omitidos. La prueba de envío **real** de Gmail se omite salvo autorización
explícita; no es necesaria para la suite local. Los demás casos de correo usan
puertos simulados y configuración en memoria.

## Vehículos

```powershell
dotnet build TallerMecanico.csproj
# Configura TALLER_TEST_MYSQL con la cadena del servidor de pruebas.
dotnet test tests/TallerMecanico.IntegrationTests/TallerMecanico.IntegrationTests.csproj --filter 'FullyQualifiedName~Vehiculos'
```

Sin `TALLER_TEST_MYSQL`, las pruebas MySQL aparecen como omitidas; las de lógica no requieren base de datos. `python tests/vehiculos_smoke.py` ejecuta esa misma selección de pruebas xUnit.

Se verifican placas nacionales de 3/4 números y 3 letras, placas extranjeras, normalización, duplicados, catálogo alfabético, cliente obligatorio y existente, borradores de sesión, retorno del registro del cliente, migración y persistencia.

## Navegador

El recorrido real requiere Node.js, Microsoft Edge y Playwright:

```powershell
npm.cmd install --prefix (Join-Path $env:TEMP 'vehiculos-browser-check') playwright --no-audit --no-fund
$env:PLAYWRIGHT_MODULE = Join-Path $env:TEMP 'vehiculos-browser-check/node_modules/playwright'
# Configura también TALLER_TEST_MYSQL.
dotnet test tests/TallerMecanico.IntegrationTests/TallerMecanico.IntegrationTests.csproj --filter 'FullyQualifiedName~VehiculosNavegadorTests'
```

La prueba inicia un servidor HTTPS temporal con certificado propio, crea un usuario Recepcionista aislado y recorre login, selección de marca/modelo, toggle, cliente obligatorio, registro y cancelación de cliente, recuperación del borrador, duplicados y edición. Después cierra servidor, navegador y base temporal. No requiere cambiar la confianza del sistema ni enviar correos.

`tests/vehiculos_ui.cjs` lo ejecuta la prueba xUnit con su configuración temporal; no inicia una aplicación SQLite ni debe apuntarse a una base del taller.

## Comprobantes PDF

La aplicación utiliza QuestPDF 2026.9.1 y Humanizer.Core.es 3.0.10. La licencia Community de QuestPDF se configura en el arranque para este uso académico individual. Antes de destinar el proyecto a otro uso, revisar las condiciones de licencia vigentes.

```powershell
dotnet build
dotnet test tests/TallerMecanico.IntegrationTests/TallerMecanico.IntegrationTests.csproj --filter 'FullyQualifiedName~Comprobante|FullyQualifiedName~ImporteEnPalabras|FullyQualifiedName~ConsultaOrdenes'
```

La conversión a palabras, la autorización, los errores y la generación básica no requieren MySQL. Las pruebas de persistencia y HTTPS usan `TALLER_TEST_MYSQL` y bases `autotaller_test_<guid>`, nunca `taller_mecanico`.

Para comprobar texto, acentos y paginación, configurar `TALLER_PDF_PYTHON` con un ejecutable Python que tenga `pypdf`. Es una herramienta externa de verificación, no una dependencia de la aplicación. Sin esa configuración, las dos pruebas específicas de contenido aparecen como omitidas. `tests/pdf_text.py` extrae el texto de documentos temporales y no modifica el PDF.

Opcionalmente, `TALLER_PDF_MUESTRAS` indica una carpeta temporal donde guardar los dos comprobantes sintéticos para revisión visual. `PLAYWRIGHT_MODULE` habilita también el recorrido en Edge del listado y modal, incluida la acción Comprobante. Las pruebas HTTPS crean su propio certificado; no cambian la confianza del sistema.

## Órdenes de la tarjeta 4

```powershell
# Configura TALLER_TEST_MYSQL con el servidor de pruebas.
dotnet test tests/TallerMecanico.IntegrationTests/TallerMecanico.IntegrationTests.csproj --filter 'FullyQualifiedName~Ordenes'
```

Comprueban creación y anulación atómicas, cantidades agrupadas, stock, auditoría, rollback después de cambios reales, reenvíos simultáneos, competencia por stock, anulaciones concurrentes, precios manipulados, claves foráneas y conservación del trigger de historial. Consultar `docs/TARJETA4_ORDENES_EC2.md` para los contratos de integración con la tarjeta 3.

## Pruebas existentes de autenticación

Las pruebas de repositorios, confirmación de correo, límite de reenvío, renovación
de token y revisión de solicitudes usan ahora `MySqlAisladoFixture`. Cada caso
prepara sus cuentas y solicitudes y elimina su base temporal al finalizar.

`EnvioGmailRealTests` es la única prueba que puede leer los secretos de SMTP y
contactar un servicio externo: aparece omitida salvo que una persona autorice el
envío y configure `AUTOTALLER_PRUEBA_SMTP=SI` y `AUTOTALLER_SMTP_DESTINATARIO`.
No habilitarla en una ejecución automática ordinaria.
