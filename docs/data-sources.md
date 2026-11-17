# Fuentes de datos y limitaciones

Todas las fuentes son de **solo lectura** y funcionan sin permisos de administrador.

| Dato | Fuente | Limitaciones conocidas |
|---|---|---|
| Nombre/versión de Windows | WMI `Win32_OperatingSystem.Caption/BuildNumber`; registro `HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion` (`EditionID`, `DisplayVersion`, `UBR`) | `ProductName` del registro dice «Windows 10» en Windows 11; por eso no se usa. |
| Arquitectura | `RuntimeInformation.OSArchitecture` | — |
| Fabricante/modelo, tipo de equipo | WMI `Win32_ComputerSystem`, `Win32_SystemEnclosure.ChassisTypes` (SMBIOS) | Algunos firmwares devuelven textos de relleno («To Be Filled By O.E.M.»): se muestran como «No disponible». |
| CPU | WMI `Win32_Processor`; `GetLogicalProcessorInformationEx(RelationProcessorCore)` para núcleos híbridos (`EfficiencyClass`) | `MaxClockSpeed` es la frecuencia base informada por el firmware, no la de turbo. |
| GPU | **DXCore** (`IsIntegrated`, `IsHardware`, `DedicatedAdapterMemory`, `SharedSystemMemory`, `DriverVersion`); WMI `Win32_VideoController.DriverDate` | `Win32_VideoController.AdapterRAM` es un entero de 32 bits (máximo 4 GiB) y no distingue memoria dedicada: **no se usa**. Si DXCore no está disponible (Windows anterior a 10 2004), la memoria y el tipo se marcan «No compatible». |
| RAM | `GlobalMemoryStatusEx`, `GetPhysicallyInstalledSystemMemory`, WMI `Win32_PhysicalMemory` | Velocidad en MT/s según SMBIOS; algunos equipos no la informan. No se lee `SerialNumber`. |
| Discos físicos | WMI `MSFT_PhysicalDisk` (`root\Microsoft\Windows\Storage`) | `MediaType` puede ser «Unspecified» (por ejemplo, en máquinas virtuales o RAID): se muestra como «Tipo no informado», sin deducirlo del bus. |
| Volúmenes | `System.IO.DriveInfo`; `MSFT_Partition` para asociar letra → disco | La etiqueta del volumen no se recopila (puede contener nombres personales). |
| Energía | `GetSystemPowerStatus`, `PowerGetActiveScheme`, `PowerEnumerate`, `PowerGetEffectiveOverlayScheme` | En equipos con Modern Standby suele existir solo el plan «Equilibrado». Si el GUID del modo de energía no es uno de los publicados, se muestra tal cual. |
| Pantallas | `EnumDisplayDevices`, `EnumDisplaySettings(ENUM_CURRENT_SETTINGS)` | Frecuencia en hercios enteros (59,94 Hz aparece como 59 o 60). |
| Programas de inicio | Claves `Run` (HKCU, HKLM 64 y 32 bits) y carpetas de Inicio; estado en `Explorer\StartupApproved` | El formato de `StartupApproved` **no está documentado**; solo se reconocen los valores observados habitualmente y el resto se muestra como desconocido. No incluye tareas programadas, servicios ni apps empaquetadas. |
| Procesos | `System.Diagnostics.Process` | Procesos protegidos: «Acceso denegado». No se leen rutas ni líneas de comandos. |

## Métricas de la sesión

Contadores PDH añadidos con `PdhAddEnglishCounter`, así que funcionan con Windows en cualquier idioma.

| Métrica | Contador | Tipo | Cálculo |
|---|---|---|---|
| Uso de CPU (%) | `\Processor Information(_Total)\% Processor Utility` | Derivado | `min(100, valor)`, igual que el Administrador de tareas |
| Frecuencia efectiva estimada (MHz) | `\Processor Information(_Total)\% Processor Performance` | Derivado | `valor / 100 × MaxClockSpeed`. Es una estimación. |
| Memoria disponible (MiB) | `\Memory\Available MBytes` | Observado | — |
| Memoria comprometida (%) | `\Memory\% Committed Bytes In Use` | Observado | — |
| Actividad del almacenamiento (%) | `\PhysicalDisk(_Total)\% Idle Time` | Derivado | `100 − % Idle Time` (media de todos los discos) |
| Transferencia (MB/s) | `\PhysicalDisk(_Total)\Disk Bytes/sec` | Observado | `÷ 1 000 000` |
| Uso de GPU (%) por adaptador | `\GPU Engine(*)\Utilization Percentage` | Derivado | Suma por motor de todos los procesos y máximo entre motores. Instancias asociadas por LUID de DXCore. |
| Memoria de GPU en uso (MiB) | `\GPU Adapter Memory(*)\Dedicated Usage` / `Shared Usage` | Observado | `÷ 1024²` |
| Temperaturas | — | No compatible | Ver abajo |

La primera lectura de los contadores de tasa no tiene valor, así que el muestreador hace una lectura de calentamiento antes de empezar.

Las muestras se toman en instantes fijos (`inicio + k × intervalo`). Los percentiles usan el método del rango más cercano (*nearest-rank*).

## Temperaturas: por qué no están

- Windows no ofrece una API oficial y fiable de temperatura de la CPU. `MSAcpi_ThermalZoneTemperature` necesita administrador y en muchos equipos devuelve valores ausentes o de una zona ACPI que no es la CPU.
- Las bibliotecas que leen sensores reales (por ejemplo, LibreHardwareMonitor, MPL-2.0) dependen de un **controlador de kernel**. El controlador WinRing0, que usaban muchas de estas herramientas, tiene una vulnerabilidad de escalada de privilegios (CVE-2020-14979) y Microsoft Defender lo bloquea.
- Decisión de la fase 1: **no instalar controladores de bajo nivel**. Más adelante se valorará usar las API de los fabricantes de GPU (NVAPI, ADLX, IGCL) para la temperatura de la GPU, revisando su licencia y mantenimiento.
