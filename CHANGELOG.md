# Registro de cambios

El formato sigue [Keep a Changelog](https://keepachangelog.com/es-ES/1.1.0/) y el proyecto usa [versionado semántico](https://semver.org/lang/es/).

## [Sin publicar]

### Añadido (fase 1: diagnóstico de solo lectura)
- Inventario automático: Windows, equipo y tipo (escritorio/portátil), CPU (con núcleos híbridos), GPU integrada/dedicada y memoria dedicada/compartida (DXCore), controladores, RAM y módulos, discos físicos y volúmenes por separado, energía y batería, plan y modo de energía, pantallas y programas de inicio.
- Sesiones de medición configurables: CPU, frecuencia estimada, memoria, almacenamiento, GPU por adaptador y procesos, con unidad, marca de tiempo, origen y estado de cada muestra.
- Interfaz WPF con tema oscuro, en español e inglés: Inicio, Mi equipo, Diagnóstico, Historial y Configuración y ayuda. Las secciones pendientes están señaladas.
- Exportación JSON o texto con vista previa y anonimización.
- Historial local limitado, que se puede borrar.
- Pruebas unitarias y de integración (solo lectura).

### Limitaciones conocidas
- Sin temperaturas (no hay una fuente compatible sin controladores de kernel).
- Sin probar todavía en hardware real; ver `docs/testing.md`.
