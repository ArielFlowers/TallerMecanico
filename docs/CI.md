# CI por aplicación

Cada aplicación tiene un solo check y solo corre cuando cambia algo que la afecta.
Las pruebas que necesitan las cuatro aplicaciones juntas tienen su propio check.

| Check | Qué comprueba | Se ejecuta si cambia |
|---|---|---|
| [Frontend / Pruebas](../.github/workflows/ci-frontend.yml) | Páginas y lógica con los servicios simulados | `frontend-app/` o sus pruebas |
| [Servicio Ordenes / Pruebas](../.github/workflows/ci-servicio-ordenes.yml) | Dominio, casos de uso, catálogos y MySQL | `servicio-ordenes/`, sus pruebas o `docs/migraciones/` |
| [Servicio Usuarios / Pruebas](../.github/workflows/ci-servicio-usuarios.yml) | Cuentas, contraseñas, correo simulado y MySQL | `servicio-usuarios/`, sus pruebas o el esquema del Servicio 1 |
| [Servicio Reportes / Pruebas](../.github/workflows/ci-servicio-reportes.yml) | Consultas, PDF, Excel y MySQL | `servicio-reportes/`, sus pruebas o el esquema del Servicio 1 |
| [Integracion / HTTP y navegador](../.github/workflows/ci-integracion.yml) | HTTP real entre las apps y recorridos en Edge | Cualquier app, `shared/` o `tests/` |
| [Infraestructura / Docker y lanzadores](../.github/workflows/ci-infraestructura.yml) | Las cuatro imágenes Docker y los lanzadores | Docker, scripts o cualquier app |

Los cambios en `shared/`, `tests/comun/` o las acciones comunes disparan las cuatro
aplicaciones. Un cambio solo de documentación no ejecuta ningún check. Los pasos
comunes de cada aplicación están en
[probar-app](../.github/actions/probar-app/action.yml).

## Cuando se ejecuta

- Al abrir o actualizar un PR, si cambió una ruta vigilada.
- Al hacer push a `main`, con las mismas rutas.
- Manualmente desde Actions, mediante `workflow_dispatch`.
- Un nuevo commit del mismo PR cancela la ejecución anterior de ese workflow.

Si en GitHub se marcan checks como obligatorios, no conviene exigir los de un workflow
con rutas: cuando no corre, el PR queda esperando un check que nunca llega.

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

Cada aplicación ejecuta `Area=Backend|Area=Datos`. Integracion ejecuta
`Area=API|Area=Frontend|Tipo=Navegador` sobre toda la solución, con Playwright habilitado.

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
Las aplicaciones e Integracion comparten la preparacion de Python y pypdf mediante
[preparar-pdf](../.github/actions/preparar-pdf/action.yml).

El check de Infraestructura construye las imágenes y simula Docker en los lanzadores; no
levanta el entorno. CI usa exclusivamente MySQL temporal, nunca Aiven.
