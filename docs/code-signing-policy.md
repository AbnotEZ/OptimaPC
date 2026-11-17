# Política de firma de código / Code signing policy

Free code signing provided by [SignPath.io](https://about.signpath.io), certificate by [SignPath Foundation](https://signpath.org).

> Estado: solicitud pendiente. Mientras no se apruebe, las versiones se publican sin firmar y llevan `-unsigned` en el nombre del archivo.

## Qué se firma

- Solo los binarios compilados a partir de este repositorio, en GitHub Actions (`.github/workflows/release.yml`), a partir de una etiqueta de versión.
- Actualmente: `OptimaPC.exe`. Las bibliotecas nativas de WPF incluidas en el paquete ya vienen firmadas por Microsoft y no se vuelven a firmar.
- Nunca se firman binarios compilados en equipos personales ni código de terceros que no forme parte del proyecto.

## Funciones del equipo / Team roles

| Función | Personas |
|---|---|
| Committers y revisores | `AbnotEZ` |
| Aprobadores (aprueban cada solicitud de firma en SignPath) | `AbnotEZ` |

Todas las personas con estas funciones usan autenticación de dos factores en GitHub y en SignPath.

## Proceso

1. Una persona aprobadora crea la etiqueta `v<versión>` en `main`.
2. El workflow compila, ejecuta las pruebas y envía el paquete a SignPath.
3. Una persona aprobadora revisa y aprueba la solicitud de firma en SignPath.
4. El workflow comprueba la firma, calcula los SHA-256 y crea un **borrador** de Release que se revisa antes de publicarlo.

## Privacidad / Privacy policy

This program will not transfer any information to other networked systems unless specifically requested by the user or the person installing or operating it.

Este programa no transfiere información a otros sistemas en red salvo que lo solicite expresamente quien lo usa, instala o maneja. Ver [privacy.md](privacy.md).
