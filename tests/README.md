# Pruebas de vehículos

## Compatibilidad con esta rama MySQL

Los scripts descritos a continuación se conservan de la etapa SQLite. **No deben ejecutarse sobre la aplicación actual sin adaptarlos a una base MySQL aislada.** Configuran `DefaultConnection` para SQLite, mientras la aplicación actual usa `MySqlConnection`; por ello no garantizan aislamiento de los datos de MySQL. La opción `--include-mysql-pages` también pertenece a la etapa anterior, cuando solo servicios y dashboard requerían MySQL.

Para esta rama, `dotnet build` verifica la compilación. La prueba funcional requiere preparar explícitamente una base MySQL de pruebas y adaptar los scripts. Las instrucciones siguientes documentan su ejecución histórica con SQLite.

Las pruebas arrancan la aplicación con bases SQLite temporales y eliminan esos datos al finalizar. No utilizan la base del taller.

## Servidor y persistencia

Requiere .NET 10 y Python 3, sin paquetes adicionales:

```powershell
dotnet build
python tests/vehiculos_smoke.py
```

Comprueba normalización, formato de placa, duplicados, errores de conversión, catálogo, CRUD, búsqueda, las páginas de mecánicos e historial y migración de bases antiguas tras dos arranques. El CRUD se ejecuta a través del registro real de `CreadorVehiculos` en la inyección de dependencias.

El dashboard y servicios ahora requieren MySQL. Sus comprobaciones son opcionales y los errores no se ignoran al activarlas. Para incluirlas, configura `ConnectionStrings__MySqlConnection` con una base de pruebas preparada con el esquema de servicios y ejecuta:

```powershell
python tests/vehiculos_smoke.py --include-mysql-pages
```

Esta opción consulta la base MySQL configurada; no crea ni elimina esa base.

## Navegador

Requiere Node.js, Microsoft Edge y Playwright. Para instalar la dependencia fuera del repositorio y ejecutar:

```powershell
npm install --prefix "$env:TEMP/vehiculos-browser-check" playwright --no-audit --no-fund
$env:PLAYWRIGHT_MODULE = Join-Path $env:TEMP 'vehiculos-browser-check/node_modules/playwright'
node tests/vehiculos_ui.cjs
```

Comprueba los desplegables dependientes, las advertencias de placa, la conservación de selecciones después de un error y los modales de edición y eliminación.
