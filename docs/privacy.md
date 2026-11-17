# Privacidad

- **Local.** Todo se procesa en el equipo. La aplicación no realiza conexiones de red.
- **Sin cuenta, publicidad ni telemetría.**
- **Qué no se recopila:** números de serie, claves de producto, identificadores de hardware únicos, etiquetas de volumen, rutas o líneas de comandos de los procesos.
- **Qué se guarda localmente** (`%LocalAppData%\OptimaPC`):
  - historial de análisis (inventario + mediciones), con límite de 30 registros o 25 MiB;
  - registro de errores, pasado por el anonimizador antes de escribirse;
  - configuración.
- **Borrado:** «Configuración y ayuda → Borrar historial y registros», o «Historial → Eliminar / Eliminar todo».
- **Exportación voluntaria con vista previa.** Lo que se ve en la vista previa es exactamente lo que se guarda. Antes de escribir el archivo:
  - el nombre de usuario se sustituye por `<USER>`, el del equipo por `<COMPUTER>` y el dominio por `<DOMAIN>`;
  - la ruta del perfil se sustituye por `%USERPROFILE%`, y cualquier otra ruta `X:\Users\<nombre>` por `X:\Users\<USER>`;
  - los comandos de los programas de inicio se **excluyen por defecto** (pueden contener rutas o nombres que prefieras no compartir);
  - la lista de procesos se puede excluir.
- **Limitación honesta:** el anonimizador trabaja sobre identificadores conocidos y patrones de ruta. Otros datos que hayas puesto tú (por ejemplo, el nombre de un programa) pueden seguir apareciendo. Revisa la vista previa antes de compartir.
