# Arquitectura

## Proyectos

| Proyecto | Responsabilidad | Depende de |
|---|---|---|
| `PcToolkit.Core` (`net10.0`) | Modelos (`Reading<T>`, inventario, muestras), interpretación de códigos, orquestación del inventario, sesiones de muestreo, cálculo por proceso, privacidad, exportación, historial, configuración, textos (es/en) y presentación. **Sin APIs de Windows.** | — |
| `PcToolkit.Windows` (`net10.0-windows`) | Adaptadores reales de **solo lectura**: WMI/CIM, PDH, DXCore, Win32 (memoria, energía, pantallas), registro (lectura). | Core, System.Management |
| `PcToolkit.App` (`net10.0-windows`, WPF) | Interfaz MVVM, tema Fluent oscuro. Sin lógica de negocio. Manifiesto `asInvoker`. | Core, Windows, CommunityToolkit.Mvvm |
| `tests/PcToolkit.Core.Tests` | Pruebas unitarias con fakes. No acceden al sistema (solo carpetas temporales). | Core |
| `tests/PcToolkit.Windows.IntegrationTests` | Pruebas de integración de **solo lectura** contra el Windows real. | Windows |

Fases posteriores añadirán `PcToolkit.Changes` (motor de operaciones), `PcToolkit.Elevated` (auxiliar elevado con lista blanca) y `PcToolkit.GameCapture`. No existen todavía.

## Principios de diseño

- **Puertos y adaptadores.** Core define `IInventorySources`, `IPerformanceSampler(Factory)`, `IProcessSnapshotSource` e `ISessionClock`; Windows los implementa. Toda la lógica se prueba con fakes, sin tocar el sistema.
- **Datos con estado y origen.** Cada valor es un `Reading<T>` con `Status` (`Available`, `NotAvailable`, `NotSupported`, `AccessDenied`, `Failed`), `Source` y `Detail`. Nunca se sustituye un dato ausente por un valor por defecto.
- **Muestras explícitas.** `Sample` tiene marca de tiempo y estado (`Valid`, `Missing`, `Invalid`). Las estadísticas cuentan las ausentes y no las interpolan.
- **Observado frente a derivado.** Cada métrica declara si se lee directamente o se calcula, con la fórmula.
- **Aislamiento de fallos.** El inventario ejecuta cada área en paralelo; si una falla, se marca `Failed` y las demás continúan.
- **Nombre centralizado.** `<Product>` en `Directory.Build.props`; el código lo lee con `AssemblyProductAttribute` (`AppInfo`).
- **Textos centralizados.** `Core/Localization/Strings.{es,en}.json`. Una prueba verifica que ambos idiomas tienen las mismas claves y marcadores, y que toda clave usada en el código existe.

## Datos locales

`%LocalAppData%\OptimaPC\`

- `history\analysis-<id>.json`: análisis completos. Máximo 30 registros o 25 MiB; se borran los más antiguos. Escritura atómica (archivo temporal + renombrado).
- `logs\app.log`: registro de errores, anonimizado, rotación a 1 MiB × 3.
- `settings.json`.

Los futuros respaldos de restauración (fase 3) irán en una ubicación separada del historial, para que borrar el historial nunca afecte a la capacidad de restaurar.

## Presupuesto de rendimiento de la propia aplicación

| Recurso | Presupuesto | Cómo se verificará | Estado |
|---|---|---|---|
| Arranque en frío hasta ventana (SSD) | < 2 s | Cronometraje con `Stopwatch` + registro | Pendiente de medir |
| Memoria en reposo (working set) | < 150 MiB | Administrador de tareas / `dotnet-counters` | Pendiente de medir |
| CPU en reposo sin medición | ≈ 0 % | No hay temporizadores activos fuera de una sesión | Por diseño; pendiente de medir |
| CPU durante una medición a 1 s | < 1 % de la CPU total | La propia sesión mide el proceso de la app | Pendiente de medir |
| Disco | Sin escrituras durante la medición; historial ≤ 25 MiB | Por diseño | Por diseño |

Decisiones que lo apoyan: una sola consulta PDH por sesión; procesos leídos solo al principio y al final de la sesión (no en cada muestra); inventario bajo demanda; ningún servicio ni inicio con Windows.
