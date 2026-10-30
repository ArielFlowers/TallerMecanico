> **Registro fechado anterior a la retirada del modo local.** Los resultados y comandos históricos se conservan como evidencia. El flujo vigente usa solo Aiven; consultar [README.md](../README.md) y [VALIDACION_NUBE_UNICA.md](VALIDACION_NUBE_UNICA.md).

# Corrección y validación de las pruebas

Revisión del 29-30 de octubre de 2026, posterior a la simplificación de la
infraestructura. Los cambios de esta revisión se limitan a las pruebas y su
documentación; no cambian el C# de la aplicación ni las reglas del negocio.

## Resultado y antecedentes

La ejecución completa del **30/10/2026** terminó con **515 casos: 514 aprobados,
0 fallidos y 1 omitido**, en 6 minutos y 5 segundos. Compilaron la aplicación y el
proyecto de pruebas. Se ejecutaron los recorridos de Edge y las comprobaciones
de contenido PDF; la única omisión es el envío externo real de Gmail.

Los **13 casos que fallaban en la reproducción inicial ahora están aprobados**.
La comparación de los TRX confirmó que ninguno de los 505 casos de esa
reproducción desapareció. Los diez casos adicionales corresponden a las clases
de autenticación/repositorios y al envío manual que antes estaban excluidos.

El informe histórico del 27-28/10 en [VALIDACION_MYSQL84.md](VALIDACION_MYSQL84.md)
conserva sus resultados: 505 casos, 487 aprobados, 13 fallidos y 5 omitidos, con
seis clases excluidas. Esos resultados no se sustituyen por los de esta revisión.

La reproducción del 29/10, con Edge, Playwright y Python para PDF habilitados,
registró 505 casos: 492 aprobados, 13 fallidos y ninguno omitido. Las correcciones
se comprobaron primero por grupos y después con una ejecución completa sin
exclusiones de clases.

## Qué se corrigió

| Área | Corrección comprobada |
|---|---|
| Órdenes y totales | El total incluye servicios y repuestos. Se verifican ambos detalles por separado y se manipulan los precios enviados para comprobar que prevalecen los de la base. |
| Selección de servicios | Las validaciones y borradores usan `ServicioIds`; se conserva la comprobación de migración del identificador antiguo. |
| Autocompletado | Las consultas iniciales, los límites del catálogo y la propiedad `stock` coinciden con los contratos actuales. Se mantienen los rechazos de entradas inválidas. |
| Interfaz de órdenes | Los recorridos usan los filtros actuales, la confirmación antes de guardar y el comprobante en modal con iframe. Comprueban cancelación, errores de stock/PDF, reenvío, idempotencia, recarga y permisos. |
| Navegación y borradores | Se comprueban los retornos compatibles entre CRUD y la conservación de la selección; las comprobaciones móviles desplazan el campo al área visible antes de comprobar sugerencias. |
| Migraciones | La preparación del caso respeta el orden de eliminación de tablas dependientes por claves foráneas. |
| Autenticación y repositorios | Las cinco clases que dependían de cuentas o conexiones de User Secrets preparan ahora sus propios datos con `MySqlAisladoFixture`. |
| Prerrequisitos | Los casos de navegador indican una omisión explícita si faltan sus herramientas. La prueba de Gmail real deja de contabilizar un retorno temprano como aprobado. |

## Aislamiento y reproducción

Se utilizó un MySQL 8.4 exclusivo de pruebas, con almacenamiento temporal y un
puerto publicado en loopback. Cada fixture crea y elimina su propia base
`autotaller_test_<guid>`. La fixture rechaza servidores remotos antes de abrir la
conexión. Las cuentas y solicitudes del taller no se usan como datos de prueba.

La ejecución completa configura `TALLER_TEST_MYSQL`, `PLAYWRIGHT_MODULE` y
`TALLER_PDF_PYTHON`, y mantiene deshabilitado `AUTOTALLER_PRUEBA_SMTP`.
La preparación reproducible está en [tests/README.md](../tests/README.md).

```powershell
dotnet test tests/TallerMecanico.IntegrationTests/TallerMecanico.IntegrationTests.csproj --logger "trx;LogFileName=integracion.trx" --results-directory TestResults -- xUnit.ParallelizeTestCollections=false
```

El script Linux de regresión ya no excluye las clases antiguas de autenticación;
su sintaxis se comprobó con `sh -n`. Las herramientas opcionales no configuradas
en ese entorno seguirán apareciendo como omitidas.

La única prueba externa manual es el envío real de Gmail. Requiere autorización
de una persona, `AUTOTALLER_PRUEBA_SMTP=SI` y un destinatario explícito. Los demás
casos de correo usan implementaciones simuladas. No se envían correos reales en
la ejecución local ordinaria.

## Evidencia y límites

La evidencia queda en `TestResults/pruebas-correccion/`, ignorada por Git:
`baseline.trx`, resultados por grupos, `completa.trx`, `completa.log`, y snapshots
de conteos/checksums locales antes y después. Las credenciales temporales no se
publican en este documento.
`resumen.json` conserva los contadores finales y la comparación de los 13 casos
fallidos anteriores con sus resultados aprobados.

La comparación final con el snapshot anterior a las correcciones confirmó
conteos y checksums iguales en las **doce tablas locales**: se conservan 154
filas de negocio, 4 cuentas, cero solicitudes y 8 auditorías. Los hashes de
`.env` y `bd-nube.env` también coinciden. El resultado está en `conservacion.json`.

Al finalizar no quedaba ninguna base `autotaller_test_<guid>` en el servidor de
pruebas. Se retiró su contenedor temporal. El MySQL local, que estaba detenido
al iniciar este cierre, se abrió solo para consultar conteos/checksums y se
devolvió al estado detenido, conservando su volumen. `limpieza.json` registra
estas comprobaciones.

El resultado y los registros históricos se conservan en esta documentación.
Los PDF generados se retiraron después por decisión del equipo, que usa el
README como guía principal. `git diff --check` terminó sin errores.

Una ejecución interrumpida y un intento sin Docker disponible no constituyen
la validación final. Tras recuperar Docker Desktop se actualizó el puerto del
contenedor temporal. Al no estar disponible el SDK del sistema, se preparó una
copia local del SDK 10.0.401 con SHA-512 contrastado con los
[metadatos oficiales de Microsoft](https://builds.dotnet.microsoft.com/dotnet/release-metadata/10.0/releases.json).
Ese auxiliar permanece en `TestResults/` y no cambia los requisitos del proyecto.

No se ejecutan pruebas ni importaciones contra Aiven. La comprobación con un
compañero fue confirmada por el usuario como realizada antes de simplificar;
esta ejecución no pretende repetirla ni validar carga o recuperación de Aiven.
