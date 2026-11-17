# Contribuir

Gracias por tu interés. Este proyecto prioriza la **honestidad de los datos** y la **seguridad del equipo del usuario** por encima de las funciones nuevas.

## Antes de abrir un PR

1. Abre una incidencia para hablar de cambios grandes.
2. `dotnet build OptimaPC.slnx` sin advertencias (se tratan como errores).
3. `dotnet test tests/PcToolkit.Core.Tests`.
4. Si añades textos, añádelos en `Strings.es.json` **y** en `Strings.en.json`; una prueba verifica que ambos tienen las mismas claves.

## Reglas del proyecto

- No inventes datos. Si una fuente no devuelve un valor, usa `Reading<T>.Missing(...)` con el estado adecuado.
- Toda métrica declara unidad, origen y si es observada o derivada (con la fórmula).
- Nada de puntuaciones de «salud», alarmas, ni promesas de mejora de rendimiento.
- La lógica va en `PcToolkit.Core` y se prueba con fakes; las llamadas al sistema van en `PcToolkit.Windows`.
- En esta fase no se aceptan cambios que escriban en el sistema. Los ajustes de la fase 3 necesitan documentación, restauración y pruebas en un entorno desechable.
- No se aceptan controladores de kernel, ajustes de temporizadores, red, prioridades extremas ni «limpiadores de registro».
- No incluyas secretos, certificados ni datos personales en el repositorio.
- Pruebas: nunca escribas una prueba que modifique el Windows de quien la ejecuta.

## Estilo

`.editorconfig` en la raíz. Espacios de nombres con ámbito de archivo y nombres en inglés en el código. Los comentarios pueden ir en español.
