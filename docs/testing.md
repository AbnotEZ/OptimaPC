# Pruebas y estado de verificación

## Política

- Las pruebas unitarias usan fakes y **no modifican el sistema**.
- Las pruebas de integración son de **solo lectura** y comprueban coherencia, no valores de un equipo concreto.
- Ninguna prueba aplica optimizaciones. Las pruebas que modifiquen Windows (fase 3) solo se ejecutarán en un entorno desechable autorizado (máquina virtual o runner de CI).

## Cómo ejecutar

```powershell
dotnet test tests/PcToolkit.Core.Tests                 # unitarias
dotnet test tests/PcToolkit.Windows.IntegrationTests   # integración, solo lectura
```

### Smart App Control

Con Smart App Control **activado**, Windows bloquea la carga de las DLL de prueba recién compiladas y sin firma (error `0x800711C7`, eventos 3033/3077 en `Microsoft-Windows-CodeIntegrity/Operational`). Opciones, sin desactivar la protección:

1. Ejecutar las pruebas en GitHub Actions (workflow `ci.yml`, runner Windows desechable).
2. Usar una máquina virtual con Windows 11 (VirtualBox o VMware en Windows Home, que no incluye Hyper-V ni Windows Sandbox).

## Estado actual (v0.1.0)

| Área | Implementado | Prueba automática escrita | Prueba automática ejecutada | Probado a mano | Validado en hardware real |
|---|---|---|---|---|---|
| Modelo `Reading`/muestras y estadísticas | Sí | Sí | Sí (CI) | No | — |
| Mapas de hardware (chasis, SMBIOS, almacenamiento, PCI) | Sí | Sí | Sí (CI) | No | No |
| Agregación de contadores de GPU (varias GPU) | Sí | Sí | Sí (CI) | No | No |
| `StartupApproved` | Sí | Sí | Sí (CI) | No | No |
| Sesión de muestreo (cancelación, muestras ausentes y erróneas) | Sí | Sí | Sí (CI) | No | No |
| CPU por proceso (reutilización de PID, acceso denegado) | Sí | Sí | Sí (CI) | No | No |
| Aislamiento de fallos del inventario | Sí | Sí | Sí (CI) | No | No |
| Anonimizador y exportación | Sí | Sí | Sí (CI) | No | — |
| Historial (retención, archivos dañados o incompatibles, rutas no válidas) | Sí | Sí | Sí (CI) | No | — |
| Textos es/en (mismas claves y marcadores, claves usadas) | Sí | Sí | Sí (CI) | No | — |
| Inventario real de Windows (WMI, DXCore, Win32) | Sí | Sí (integración) | Sí (CI, Windows Server sin GPU) | No | No |
| Muestreo PDH real | Sí | Sí (integración) | Sí (CI, Windows Server sin GPU) | No | No |
| Interfaz WPF | Sí | No | — | No | No |

Las pruebas automáticas se ejecutan en GitHub Actions (`windows-latest`, Windows Server) y pasan desde el commit `96fbfcd`. Los dos primeros intentos fallaron y sirvieron para detectar y corregir dos errores reales: el orden del anonimizador y los textos colocados en ensamblados satélite. La interfaz todavía no se ha ejecutado en ningún equipo.

## Matriz de hardware pendiente

| Caso | Estado |
|---|---|
| Solo GPU integrada | Pendiente |
| GPU dedicada | Pendiente |
| Varias GPU (portátil híbrido) | Pendiente |
| Portátil con batería, enchufado y con batería | Pendiente |
| Escritorio sin batería | Pendiente |
| CPU híbrida (núcleos P/E) | Pendiente |
| Windows en español y en inglés | Pendiente |
| Escalas de pantalla 100 %, 150 % y 200 % | Pendiente |
| Varias pantallas con distintas frecuencias | Pendiente |
| Máquina virtual sin GPU (adaptador software) | Pendiente (CI) |
| Lector de pantalla (Narrador) y navegación solo con teclado | Pendiente |
