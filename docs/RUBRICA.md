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

| Tarea | Pts | Detalle | Estado actual 09/09/2026 |
|---|---|---|---|
| Home + Menú | 6 | Colores línea gráfica, varias opciones según sistema, logo propio, **NO plantilla default Razor** | ✅ En revisión: rama `feature/home-inicio` con Hero (fondo `wwwroot/images/hero-taller.jpg` desde `docs/Gemini_*.jfif` — no existía `watermarked_*.jpg`), 3 cards, banner auditoría, footer "AutoTaller Pro - v1.0", métricas conservadas. Pendiente comprobación visual antes del PR |
| Validaciones x3 tablas | 18 (6 c/u) | Lógica de negocio, clara al usuario | ⚠️ Parcial: Mecánicos OK (`ValidacionMecanicos`), Servicios básico, Tabla3 inexistente |
| CRUD Tabla1 Mecánicos | 12 | Select 3 (refresco + ordenados), Insert 4, Update 4, Delete 1 | ✅ Casi: `MecanicoRepository` ordenado + búsqueda, falta verificar refresco UI |
| CRUD Tabla2 Servicios | 12 | Igual anterior | ✅ Casi: `ServicioRepository ORDER BY Nombre`, CRUD en `/Servicios/*` |
| CRUD Tabla3 ??? | 12 | Igual anterior | ❌ Falta: definir (sugerido Vehículos/Clientes). Bloqueado – lo hacen amigos |
| Histórico 1 tabla | 7 | Vista que muestre historial cambios | ✅ Parcial: trigger `TRG_Servicios_HistorialCosto` + `/Historial`, verificar UI |
| Informe (Anexo 1) | 10 | PDF individual (mismo doc todo el equipo) | ❌ Pendiente |
| Defensa individual | 18 | Preguntas código + conceptos | ⏳ Preparar: cada uno debe explicar su parte + SOLID |
| Feedback exposición | 5 | Exponer feedback evaluación proyecto | ⏳ Pendiente |

## Anexo 1 – Contenido mínimo informe

1. Carátula.
2. Introducción: explicación SOLID + objetivo práctica.
3. Descripción proyecto: contexto/problemática + requisitos funcionales mínimos.
4. Diseño e implementación: diagrama clases, justificación diseño, **ejemplo de cada principio SOLID con justificación**.
5. Repo: URL + organización (ramas, commits).
6. Conclusiones: aprendizajes SOLID, dificultades, recomendaciones.
7. Bibliografía.

## Qué NO tocar por ahora (acuerdo 09/09/2026)

- Código de Tabla3 y pendientes de compañeros – solo correr y documentar.
- No migrar a EF, no cambiar `DatabaseInitializer`, no romper trigger histórico.

## Próximos pasos cuando se desbloquee

1. Crear Tabla3 (≥4 attrs) + `Repository` async con interfaz + `Service` + `Validator` + `InputModel` + `Pages/*` (copiar patrón Mecánicos).
2. Layout propio + logo + colores (sacar plantilla default).
3. Arreglar `DashboardService` (hoy `Mecanicos=0, Vehiculos=0` hardcodeados).
4. Verificar Select con refresco + orden en las 3 tablas.
5. Armar informe Anexo 1 + mapear SOLID:
   - S: `MecanicoService` vs `MecanicoRepository` vs `ValidacionMecanicos`.
   - O/D: `IMecanicoRepository, IHistorialCostoServicioService` inyectados en `Program.cs`.
   - L/I: interfaces pequeñas `GetAll()` histórico vs CRUD completo.
6. Preparar defensa: cada integrante explica flujo `PageModel->Service->Repository->SQLite`.

## Comandos memoria

```powershell
$env:PATH = "$env:LOCALAPPDATA\Microsoft\dotnet10;$env:PATH"
dotnet restore; dotnet build; dotnet watch run
# DB: TallerMecanico.db (borrar = reset)
```
