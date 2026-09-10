# Rúbrica – Parámetros para continuar programando

> Fuente: `ARQ EC1 1er Entregable (3).pdf` (en raíz del proyecto).
> Materia: Arquitectura de Software – UCB. Tema: Clean Code + SOLID.
> Entrega grupal + informe individual PDF por LMS. Fecha: **01/09/2026 impostergable**.
> Repo Git con acceso al docente obligatorio.

## Reglas técnicas mínimas (no negociables)

1. CRUDs completos en **3 tablas**, cada una con **≥4 atributos independientes** (sin contar PK ni auditoría).
2. Validar entradas según **lógica de negocio**, mensajes claros para usuario final.
3. **Histórico en 1 tabla**: modelo + vista que muestre cambios, pensado a futuro.
4. Stack obligado: **Razor Pages + C# + ADO.NET**. Nada de EF Core.
5. Todo con **Clean Code + SOLID**, bajo penalización.

## Rúbrica (total 100)

| Tarea | Pts | Detalle | Estado actual 10/09/2026 |
|---|---|---|---|
| Home + Menú | 6 | Colores línea gráfica, varias opciones según sistema, logo propio, **NO plantilla default Razor** | ✅ En revisión: rama `feature/home-inicio` con Hero (fondo `wwwroot/images/hero-taller.jpg` desde `docs/Gemini_*.jfif` — no existía `watermarked_*.jpg`), 3 cards, banner auditoría, footer "AutoTaller Pro - v1.0", métricas conservadas. Pendiente comprobación visual antes del PR |
| Validaciones x3 tablas | 18 (6 c/u) | Lógica de negocio, clara al usuario | ✅ Completo: Mecánicos (`ValidacionMecanicos`, Santiago), Servicios (`ValidacionServicios` + ViewModel, Aldair), Vehículos (atributos en `VehiculoFormViewModel`, Adrian). Ver resúmenes en `CLASES_CLAVE.md` |
| CRUD Tabla1 Mecánicos | 12 | Select 3 (refresco + ordenados), Insert 4, Update 4, Delete 1 | ✅ Completo (Santiago): patrón `InputModel->Service->IMecanicoRepository` async, rama `dev2/crud-mecanicos` |
| CRUD Tabla2 Servicios | 12 | Igual anterior | ✅ Completo (Aldair): `ORDER BY Nombre`, CRUD en `/Servicios/*` + `Control/Registros` + partials, `IServicioRepository`, rama `dev4/crud-servicios` (`fd67bd6`, PR #10 mergeado) |
| CRUD Tabla3 Vehículos | 12 | Igual anterior | ✅ Completo (Adrian): `Id,Placa(UNIQUE),Modelo,Kilometraje,Observaciones`, modales + buscador + orden por placa, conectó contador del Dashboard, rama `feature/us03-crud-vehiculos` (PR #11 mergeado) |
| Histórico 1 tabla | 7 | Vista que muestre historial cambios | ✅ Completo (Alex): trigger `TRG_Servicios_HistorialCosto` + `/Historial`, probado (costo sí registra, descripción/refresco no), rama `feature/us05-historial-costo-servicios` integrada a `main` |
| Informe (Anexo 1) | 10 | PDF individual (mismo doc todo el equipo) | ⏳ En proceso: resúmenes US01–US05 volcados en `CLASES_CLAVE.md` (Ariel/Santiago/Adrian/Aldair/Alex). Falta redactar PDF con carátula, intro SOLID, requisitos, diagrama + mapeo SOLID, repo, conclusiones, bibliografía |
| Defensa individual | 18 | Preguntas código + conceptos | ⏳ Preparar: cada uno explica su US + SOLID (ver mapeo en `CLASES_CLAVE.md` → Aportes por integrante) |
| Feedback exposición | 5 | Exponer feedback evaluación proyecto | ⏳ Pendiente |

## Anexo 1 – Contenido mínimo informe

1. Carátula.
2. Introducción: explicación SOLID + objetivo práctica.
3. Descripción proyecto: contexto/problemática + requisitos funcionales mínimos.
4. Diseño e implementación: diagrama clases, justificación diseño, **ejemplo de cada principio SOLID con justificación**.
5. Repo: URL + organización (ramas, commits).
6. Conclusiones: aprendizajes SOLID, dificultades, recomendaciones.
7. Bibliografía.

## Qué NO tocar por ahora (acuerdo 09/09/2026, revisado 10/09/2026)

- No migrar a EF, no cambiar `DatabaseInitializer`, no romper trigger histórico.
- Tabla3 Vehículos ya desbloqueada y mergeada (Adrian, PR #11) – solo documentar.

## Próximos pasos (actualizado 10/09/2026)

1. ✅ Tabla3 Vehículos lista (≥4 attrs + `IVehiculoRepository` async + `IVehiculoService` + ViewModel + `Pages/Vehiculos`).
2. Home en revisión (`feature/home-inicio`) – comprobar visual y fusionar.
3. Conectar `MecanicosDisponibles` en `DashboardService` (único hardcodeado restante; Vehículos/Servicios ya reales).
4. Verificar Select con refresco + orden en las 3 tablas.
5. Redactar informe Anexo 1 usando `CLASES_CLAVE.md` → Aportes por integrante + mapeo SOLID:
   - S: `MecanicoService` vs `MecanicoRepository` vs `ValidacionMecanicos` (Santiago); `ServicioService` vs repo vs `ValidacionServicios` (Aldair); capas Vehículos (Adrian); capas Historial (Alex); `DashboardService` (Ariel).
   - O/D: `IMecanicoRepository, IServicioRepository, IVehiculoRepository/IVehiculoService, IHistorialCostoServicioService` inyectados en `Program.cs`.
   - L/I: interfaces pequeñas `GetAll()` histórico vs CRUD completo.
6. Preparar defensa: cada integrante explica su US con flujo `PageModel->Service->Repository->SQLite`.

## Comandos memoria

```powershell
$env:PATH = "$env:LOCALAPPDATA\Microsoft\dotnet10;$env:PATH"
dotnet restore; dotnet build; dotnet watch run
# DB: TallerMecanico.db (borrar = reset)
```
