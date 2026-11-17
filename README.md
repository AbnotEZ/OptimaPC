# OptimaPC

[English version](README.en.md)

Aplicación de escritorio para Windows que **detecta automáticamente las características del PC y mide su uso de recursos** con datos reales de Windows. Es local, gratuita y de código abierto (MIT).

> **Estado: versión preliminar 0.1 (fase 1 de 5). Solo lectura.**
> Esta versión **no modifica** Windows, el registro ni otros programas. No incluye recomendaciones ni optimizaciones todavía.

## Qué hace y qué no

| Hace (v0.1) | No hace |
|---|---|
| Detecta CPU, GPU (integrada/dedicada), RAM y módulos, discos físicos y volúmenes, Windows, tipo de equipo, batería, plan y modo de energía, pantallas, controladores gráficos y programas de inicio. | No promete aumentos de FPS ni muestra «puntuaciones de salud». |
| Mide durante un intervalo configurable: CPU, memoria, almacenamiento, GPU y procesos, con unidad, momento y origen de cada muestra. | No inventa datos: lo que no puede leer aparece como «No disponible» o «No compatible». |
| Exporta un informe revisable (JSON o texto) que oculta usuario, nombre del equipo y rutas personales. | No envía nada a Internet. Sin cuenta, publicidad ni telemetría. |
| Guarda un historial local limitado que puedes borrar. | No cierra procesos ni cambia configuraciones. |

Las temperaturas **no se muestran** en esta versión: Windows no ofrece una fuente oficial y fiable sin instalar controladores de bajo nivel, y hemos decidido no instalarlos (ver [docs/data-sources.md](docs/data-sources.md)).

## Requisitos

- Windows 11 (versión objetivo de pruebas). Windows 10 22H2 «debería funcionar», pero no se prueba.
- x64. No requiere permisos de administrador.
- Para compilar: [.NET SDK 10](https://dotnet.microsoft.com/download) (LTS).

**Versiones de Windows probadas:** ninguna todavía en hardware real (ver [docs/testing.md](docs/testing.md)). No se afirma compatibilidad con ninguna versión concreta hasta que se pruebe.

## Capturas

Pendientes: se añadirán capturas reales cuando la aplicación se haya ejecutado en hardware real. No se usan maquetas.

## Compilar y ejecutar

```powershell
git clone https://github.com/AbnotEZ/OptimaPC.git
cd OptimaPC
dotnet build OptimaPC.slnx -c Release
dotnet test tests/PcToolkit.Core.Tests -c Release
dotnet run --project src/PcToolkit.App -c Release
```

> **Smart App Control.** Si tu Windows 11 tiene Smart App Control en modo activado, bloqueará la ejecución de binarios compilados localmente sin firma (incluidas las pruebas). No recomendamos desactivarlo: no se puede volver a activar sin reinstalar Windows. Alternativas: ejecutar las pruebas en GitHub Actions o en una máquina virtual. Ver [docs/testing.md](docs/testing.md).

## Permisos

Toda la versión 0.1 se ejecuta **sin elevación** (`asInvoker`). Algunos datos de procesos protegidos aparecen como «Acceso denegado»; es lo esperado. Detalles: [docs/permissions.md](docs/permissions.md).

## Privacidad

Procesamiento local, sin telemetría. Datos en `%LocalAppData%\OptimaPC` (historial, registros y configuración), borrables desde la aplicación. No se recopilan números de serie, claves de producto ni etiquetas de volumen. Detalles: [docs/privacy.md](docs/privacy.md).

## Hoja de ruta

1. **Diagnóstico de solo lectura** ← versión actual
2. Recomendaciones con reglas transparentes y evidencia
3. Cambios seleccionables y reversibles (motor de operaciones con respaldo, verificación y restauración)
4. Perfiles manuales y medición de juegos (FPS y tiempos de cuadro) con una integración mantenida
5. Instalador, firma de código y publicación

## Documentación

- [Arquitectura](docs/architecture.md)
- [Fuentes de datos y limitaciones](docs/data-sources.md)
- [Permisos](docs/permissions.md)
- [Privacidad](docs/privacy.md)
- [Pruebas y estado de verificación](docs/testing.md)
- [Dependencias y licencias](docs/dependencies.md)
- [Contribuir](CONTRIBUTING.md) · [Seguridad](SECURITY.md) · [Código de conducta](CODE_OF_CONDUCT.md) · [Cambios](CHANGELOG.md)

## Descargas y firma

Las versiones se publican en *Releases* como `.zip` portable, compilado en GitHub Actions, firmado y con su hash SHA-256 ([cómo verificarlo](docs/release.md#verificar-una-descarga)). No desactives ninguna protección de Windows para ejecutarlo.

Free code signing provided by [SignPath.io](https://about.signpath.io), certificate by [SignPath Foundation](https://signpath.org). Ver la [política de firma](docs/code-signing-policy.md).

## Licencia

[MIT](LICENSE). Se eligió por ser sencilla y permitir reutilizar el código en cualquier proyecto. El software se ofrece «tal cual», sin garantía de mejora de rendimiento.
