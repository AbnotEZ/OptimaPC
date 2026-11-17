# Publicar una versión

## Configuración única (la hace quien mantiene el repositorio)

1. **GitHub**
   - Activar la autenticación de dos factores en la cuenta.
   - *Settings → Environments → New environment* `release`, con **Required reviewers** (tú). Así cada firma requiere tu aprobación.
   - En ese environment: secret `SIGNPATH_API_TOKEN`.
   - *Settings → Secrets and variables → Actions → Variables*: `SIGNPATH_ORGANIZATION_ID`, `SIGNPATH_PROJECT_SLUG`, `SIGNPATH_SIGNING_POLICY_SLUG`.
   - *Settings → Rules*: proteger `main` y las etiquetas `v*` (solo quien mantiene el repositorio puede crearlas).
   - *Settings → Code security*: activar **Private vulnerability reporting** (lo usa SECURITY.md).
2. **SignPath Foundation**
   - Solicitar el programa para código abierto en <https://signpath.org>. Revisan cada proyecto, así que la aprobación no está garantizada.
   - Instalar la **GitHub App de SignPath** en el repositorio.
   - Añadir el *trusted build system* GitHub.com y vincularlo al proyecto.
   - Crear la configuración de artefacto con el contenido de `.signpath/artifact-configuration.xml`.
   - Rellenar los nombres reales de usuario en `docs/code-signing-policy.md`.

## Cada versión

1. Actualizar `<Version>` en `Directory.Build.props` y `CHANGELOG.md`.
2. Commit en `main` y etiqueta: `git tag v0.1.0` y `git push origin v0.1.0`.
3. Aprobar el despliegue del environment `release` en GitHub y la solicitud de firma en SignPath.
4. Revisar el borrador de Release (archivos, SHA256SUMS.txt y notas) y publicarlo a mano.

## Reproducir la compilación

Herramientas: .NET SDK indicado en `global.json` (10.0.x, con *roll forward* a la última versión de la banda de características) y runner `windows-latest` de GitHub.

```powershell
dotnet test tests/PcToolkit.Core.Tests -c Release
dotnet publish src/PcToolkit.App/PcToolkit.App.csproj -p:PublishProfile=win-x64
# Resultado: artifacts/publish/win-x64/
```

La compilación es determinista (`Deterministic`, `ContinuousIntegrationBuild` en CI). El binario firmado no coincidirá byte a byte con uno local, porque la firma Authenticode se añade al archivo. Para comparar, quita la firma o compara con el artefacto `unsigned-win-x64` del workflow.

## Verificar una descarga

```powershell
Get-FileHash .\OptimaPC-<versión>-win-x64.zip   # compárala con SHA256SUMS.txt
Get-AuthenticodeSignature .\OptimaPC.exe        # Status: Valid, firmante: SignPath Foundation
```

## Notas

- El paquete actual es un `.zip` portable. El instalador está previsto para la fase 5 (Inno Setup, que también habrá que firmar).
- No hay actualización automática. No se añadirá hasta poder verificar la autenticidad de las descargas.
- Firmar no garantiza que no aparezcan avisos de SmartScreen en versiones nuevas.
