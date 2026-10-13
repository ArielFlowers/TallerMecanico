# Pruebas de AutoTaller Pro

Las pruebas nuevas de vehículos usan xUnit y MySQL 8. Las pruebas de persistencia crean una base temporal `autotaller_test_<guid>` y la eliminan al finalizar; no usan ni modifican la base del taller. El usuario configurado debe poder crear y eliminar bases en el servidor de pruebas.

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

Algunas clases anteriores terminadas en `MySqlTests` utilizan User Secrets y datos de usuarios específicos. Revisar su configuración antes de ejecutarlas. `EnvioGmailRealTests` solo envía correo cuando se autoriza explícitamente mediante su variable de entorno; no forma parte de esta verificación.
