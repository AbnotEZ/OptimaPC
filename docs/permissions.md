# Permisos

## Versión 0.1 (fase 1)

- El manifiesto declara `requestedExecutionLevel level="asInvoker"`: la aplicación **nunca pide elevación**.
- Todas las operaciones son de lectura. El registro se abre siempre con `writable: false`.
- La aplicación no crea servicios, tareas programadas ni entradas de inicio, y no se inicia con Windows.
- Sin permisos de administrador, algunos datos de procesos protegidos aparecen como «Acceso denegado». Es lo esperado y no se intenta evitar.
- El botón «Abrir carpeta» de Configuración ejecuta `explorer.exe` con la ruta fija de los datos de la aplicación. Es la única vez que la aplicación inicia otro proceso.

## Fases posteriores (diseño previsto, no implementado)

- La interfaz seguirá ejecutándose sin elevación.
- Las operaciones que necesiten administrador (por ejemplo, cambios en HKLM o en planes de energía del sistema) se delegarán a un **proceso auxiliar de vida corta**, lanzado con UAC solo para esa operación. Ese auxiliar:
  - acepta solo identificadores de una lista cerrada de operaciones y parámetros tipados, que vuelve a validar;
  - nunca ejecuta comandos ni scripts arbitrarios;
  - se comunica por una canalización con nombre (*named pipe*) cuya ACL solo permite al SID del usuario que lo lanzó;
  - termina al acabar la operación.
- Los respaldos de cambios de máquina se guardarán en una ubicación protegida, con escritura solo para administradores.
- Si se deniega la elevación, la operación se cancela sin efectos parciales y se informa.

## Lo que la aplicación no tocará nunca

Microsoft Defender u otros antivirus, el firewall, Windows Update, las políticas de ejecución, servicios de forma indiscriminada, la configuración de arranque, BIOS/firmware/voltajes/overclock, archivos de juegos y sistemas antitrampas. Tampoco debilitará permisos de archivos, carpetas ni del registro.
