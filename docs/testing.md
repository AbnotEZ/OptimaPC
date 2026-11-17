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
| Modelo `Reading`/muestras y estadísticas | Sí | Sí | **No** (bloqueado por Smart App Control) | No | — |
| Mapas de hardware (chasis, SMBIOS, almacenamiento, PCI) | Sí | Sí | No | No | No |
| Agregación de contadores de GPU (varias GPU) | Sí | Sí | No | No | No |
| `StartupApproved` | Sí | Sí | No | No | No |
| Sesión de muestreo (cancelación, muestras ausentes y erróneas) | Sí | Sí | No | No | No |
| CPU por proceso (reutilización de PID, acceso denegado) | Sí | Sí | No | No | No |
| Aislamiento de fallos del inventario | Sí | Sí | No | No | No |
| Anonimizador y exportación | Sí | Sí | No | No | — |
| Historial (retención, archivos dañados o incompatibles, rutas no válidas) | Sí | Sí | No | No | — |
| Textos es/en (mismas claves y marcadores, claves usadas) | Sí | Sí | No | No | — |
| Inventario real de Windows (WMI, DXCore, Win32) | Sí | Sí (integración) | No | No | No |
| Muestreo PDH real | Sí | Sí (integración) | No | No | No |
| Interfaz WPF | Sí | No | — | No | No |

«Compila» es la única verificación que se ha hecho hasta ahora: la solución completa compila sin errores ni advertencias (`TreatWarningsAsErrors`).

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
