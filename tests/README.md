# Pruebas de vehículos

Las pruebas arrancan la aplicación con bases SQLite temporales y eliminan esos datos al finalizar. No utilizan la base del taller.

## Servidor y persistencia

Requiere .NET 10 y Python 3, sin paquetes adicionales:

```powershell
dotnet build
python tests/vehiculos_smoke.py
```

Comprueba normalización, formato de placa, duplicados, errores de conversión, catálogo, CRUD, búsqueda, páginas de los otros módulos y migración de bases antiguas tras dos arranques.

## Navegador

Requiere Node.js, Microsoft Edge y Playwright. Para instalar la dependencia fuera del repositorio y ejecutar:

```powershell
npm install --prefix "$env:TEMP/vehiculos-browser-check" playwright --no-audit --no-fund
$env:PLAYWRIGHT_MODULE = Join-Path $env:TEMP 'vehiculos-browser-check/node_modules/playwright'
node tests/vehiculos_ui.cjs
```

Comprueba los desplegables dependientes, las advertencias de placa, la conservación de selecciones después de un error y los modales de edición y eliminación.
