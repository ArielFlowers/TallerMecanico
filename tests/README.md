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

## Pruebas existentes de autenticación

Algunas clases anteriores terminadas en `MySqlTests` utilizan User Secrets y datos de usuarios específicos. Revisar su configuración antes de ejecutarlas. `EnvioGmailRealTests` solo envía correo cuando se autoriza explícitamente mediante su variable de entorno; no forma parte de esta verificación.
