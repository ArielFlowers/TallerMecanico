# CI por areas

Las cuatro suites se compilan y se ejecutan desde `TallerMecanico.sln`, manteniendo los filtros por área. Cada ensamblado guarda su propio TRX con nombre generado para evitar sobrescribir resultados.

Todo se configura en un solo [workflow CI](../.github/workflows/ci.yml). En el PR
cada check tiene un nombre que indica que esta comprobando:

| Check | Que comprueba | Herramientas preparadas por CI |
|---|---|---|
| Backend | Reglas de negocio, servicios, validaciones, sesiones y comprobantes PDF, incluido su texto. | .NET 10, Python y pypdf. |
| API / HTTP | Autenticacion, permisos, endpoints de autocompletado, formularios y respuestas HTTP/HTTPS de Razor Pages. | .NET 10 y MySQL 8.4 temporal. |
| Base de datos / MySQL | Repositorios, esquema, migraciones, transacciones, concurrencia e integridad de datos. | .NET 10 y MySQL 8.4 temporal. |
| Frontend / navegador | Interacciones reales en Edge: autocompletado, formularios, navegacion, borradores, modales y comprobantes. | .NET 10, MySQL 8.4 temporal, Node, Playwright, Python y pypdf. |
| Compatibilidad (Windows) | Compilacion y publicacion nativas, ayuda de MSBuild, lanzadores, rutas con espacios y generacion nativa de PDF. | .NET 10 y Bash de Git para Windows. |
| Compatibilidad (macOS) | Las mismas comprobaciones de compatibilidad en una maquina macOS. | .NET 10 y Bash. |

La aplicacion usa Razor Pages; **API / HTTP** incluye sus handlers y paginas,
ademas de los endpoints JSON. Backend comprueba la logica sin servidor web ni
base de datos real. Los errores de SQL se investigan primero en Base de datos;
los de rutas, cookies, roles y respuestas, en API; los de interaccion visual, en
Frontend.

## Cuando se ejecuta

- Al abrir o actualizar un PR: una ejecucion del workflow.
- Al hacer push a `main`: una ejecucion para comprobar el codigo integrado.
- Manualmente desde Actions, mediante `workflow_dispatch`.
- Un nuevo commit del mismo PR cancela su ejecucion anterior.

Los pushes a ramas de trabajo se comprueban a traves de su PR. Ya no disparan
una segunda ejecucion identica. Windows y macOS comparten una unica definicion
de job; GitHub necesita una maquina distinta para cada sistema y por eso muestra
un check de cada uno. No hay un workflow separado de portabilidad.

## Como se reparten las pruebas

Cada prueba declara exactamente un `[Trait("Area", "...")]`, en su clase o
metodo. Las clases que mezclan logica y persistencia se clasifican por metodo.

| Filtro | Contenido |
|---|---|
| `Area=Backend` | Logica sin servicios externos, mas validacion de PDF. |
| `Area=API` | Servidores HTTP/HTTPS aislados y sus contratos. |
| `Area=Datos` | Persistencia y comportamiento de MySQL. |
| `Area=Frontend` | Escenarios dedicados al navegador. |
| `Tipo=Navegador` | Escenarios HTTP que tambien contienen un recorrido de navegador. |
| `Tipo=Portabilidad` | Prueba breve de PDF que se repite en Windows y macOS. |

Frontend ejecuta `Area=Frontend|Tipo=Navegador`. Algunos escenarios existentes
preparan datos e inician sesion mediante HTTP antes de abrir el navegador:
se reutilizan con Playwright habilitado para cubrir tambien esos recorridos.
API los ejecuta sin navegador para poder identificar los fallos del servidor
por separado. Esta repeticion de preparacion es intencional; la suite completa
de backend y persistencia no se repite en cada sistema operativo.

`ClasificacionPruebasTests` impide que una prueba nueva quede sin area, pertenezca
a varias o use un nombre desconocido. Asi los filtros no dejan pruebas fuera
silenciosamente. Al agregar un recorrido de navegador a una clase HTTP, marcar
tambien `[Trait("Tipo", "Navegador")]`.

## Ejecutar un area localmente

Desde la raiz, cambiar el filtro por el area deseada:

```console
dotnet test TallerMecanico.sln -m:1 --configuration Release --filter "Area=Backend" -- xUnit.ParallelizeTestCollections=false
```

API, Datos y los escenarios completos de Frontend requieren `TALLER_TEST_MYSQL`.
La [guia principal](../README.md#pruebas-y-ci) explica como iniciar el MySQL
temporal de pruebas. No usar Aiven.

Para los recorridos de navegador:

```console
npm ci --prefix tests/browser
node tests/browser/node_modules/playwright/cli.js install msedge
```

Definir `PLAYWRIGHT_MODULE` con la ruta absoluta de
`tests/browser/node_modules/playwright`. En Linux, agregar `--with-deps` al
comando de instalacion de Edge si faltan bibliotecas del sistema. También se puede
instalar `chromium` y definir `PLAYWRIGHT_CHANNEL=chromium`; Edge sigue siendo el
valor predeterminado y el navegador de CI.

Para verificar tambien el texto de los PDF, instalar
`python -m pip install -r tests/requirements-ci.txt` y definir `TALLER_PDF_PYTHON`
con la ruta del ejecutable Python. CI prepara todas estas herramientas;
localmente las pruebas que las necesitan se omiten si no estan configuradas.

## Leer un fallo

Abrir el check del area que fallo y su paso de pruebas. Cada area guarda un
artefacto `resultados-...` con el informe TRX, incluso cuando falla la suite;
se conserva siete dias. Los pasos de preparacion comunes estan en
[preparar-pruebas](../.github/actions/preparar-pruebas/action.yml).
Backend y Frontend comparten la preparacion de Python y pypdf mediante
[preparar-pdf](../.github/actions/preparar-pdf/action.yml).

Los jobs de compatibilidad validan el software nativo y simulan Docker en los
lanzadores. El arranque de las cuatro aplicaciones Docker, HTTPS de Kestrel y la conexión
compartida se comprueban aparte; CI usa exclusivamente MySQL temporal, nunca Aiven.
