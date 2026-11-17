# Política de seguridad

## Versiones con soporte

| Versión | Soporte |
|---|---|
| 0.1.x (preliminar) | Correcciones de seguridad en la última versión |

## Cómo informar de una vulnerabilidad

**No abras una incidencia pública.** Usa el informe privado de vulnerabilidades de GitHub: pestaña *Security* → *Report a vulnerability* de este repositorio. Quien mantenga el repositorio debe activar esa opción al publicarlo.

Incluye, si puedes, la versión, los pasos para reproducirlo y el impacto. Responderemos en cuanto sea posible; al ser un proyecto voluntario, no podemos comprometernos a un plazo fijo.

## Alcance de especial interés

- Cualquier forma de hacer que la aplicación escriba en el sistema en la versión de solo lectura.
- Fugas de datos personales en informes exportados o registros.
- Problemas con rutas, enlaces simbólicos o puntos de unión en las carpetas de datos.
- En fases posteriores: el auxiliar elevado y su canal de comunicación.
