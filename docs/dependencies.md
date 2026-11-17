# Dependencias y licencias

Versiones fijadas en `Directory.Packages.props`.

## Se distribuyen con la aplicación

| Paquete | Versión | Licencia | Uso | Justificación |
|---|---|---|---|---|
| .NET 10 runtime / WPF | 10.0 (LTS, soporte hasta el 14/11/2028) | MIT | Plataforma | LTS; WPF integrado en el SDK |
| CommunityToolkit.Mvvm | 8.4.2 | MIT | MVVM (`ObservableObject`, comandos), generadores de código | Mantenido por .NET Foundation; evita código repetitivo. Paquetes firmados. |
| System.Management | 10.0.0 | MIT | Consultas WMI/CIM | Biblioteca oficial de Microsoft |

## Solo para desarrollo y pruebas

| Paquete | Versión | Licencia |
|---|---|---|
| Microsoft.NET.Test.Sdk | 17.14.1 | MIT |
| xunit | 2.9.3 | Apache-2.0 |
| xunit.runner.visualstudio | 3.1.4 | Apache-2.0 |

## Código de terceros de referencia

- Firmas y GUID de DXCore tomados de `dxcore_interface.h` (microsoft/DirectX-Headers, MIT). Solo se reproducen las declaraciones necesarias para la interoperabilidad.

## Descartadas por ahora

| Opción | Motivo |
|---|---|
| LibreHardwareMonitor (MPL-2.0) | Necesita un controlador de kernel. Ver [data-sources.md](data-sources.md). |
| Microsoft.Windows.CsWin32 | Las pocas firmas P/Invoke necesarias se declaran a mano; menos dependencias. |
| Windows App SDK / WinUI 3 | Ver la decisión WPF en el plan inicial (empaquetado más simple, menos dependencias). |
| SQLite | El historial de la fase 1 cabe en archivos JSON con escritura atómica. Se reevaluará para el diario transaccional de la fase 3. |
