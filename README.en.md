# OptimaPC

[Versión en español](README.md)

A Windows desktop application that **automatically detects your PC's hardware and measures resource usage** using real Windows data. Local, free and open source (MIT).

> **Status: preview 0.1 (phase 1 of 5). Read-only.**
> This version **does not modify** Windows, the registry or other programs. Recommendations and optimizations are not included yet.

## What it does and does not do

| Does (v0.1) | Does not |
|---|---|
| Detects CPU, GPU (integrated/dedicated), RAM and modules, physical disks and volumes, Windows, PC type, battery, power plan and mode, displays, graphics drivers and startup programs. | Promise FPS gains or show "health scores". |
| Measures over a configurable interval: CPU, memory, storage, GPU and processes, with unit, timestamp and source for each sample. | Make up data: anything it cannot read is shown as "Not available" or "Not supported". |
| Exports a reviewable report (JSON or text) that hides user name, computer name and personal paths. | Send anything to the Internet. No account, ads or telemetry. |
| Keeps a bounded local history that you can delete. | Close processes or change settings. |

Temperatures are **not shown** in this version: Windows has no official, reliable source without installing low-level drivers, and we chose not to install them (see [docs/data-sources.md](docs/data-sources.md)).

## Requirements

- Windows 11 (target test version). Windows 10 22H2 "should work" but is not tested.
- x64. No administrator rights required.
- To build: [.NET 10 SDK](https://dotnet.microsoft.com/download) (LTS).

**Windows versions tested:** none yet on real hardware (see [docs/testing.md](docs/testing.md)). No compatibility claim is made until tested.

## Screenshots

Pending: real screenshots will be added once the app has run on real hardware. No mock-ups are used.

## Build and run

```powershell
git clone https://github.com/AbnotEZ/OptimaPC.git
cd OptimaPC
dotnet build OptimaPC.slnx -c Release
dotnet test tests/PcToolkit.Core.Tests -c Release
dotnet run --project src/PcToolkit.App -c Release
```

> **Smart App Control.** If Smart App Control is on, Windows 11 blocks locally built unsigned binaries (including the tests). We do not recommend turning it off: it cannot be turned back on without reinstalling Windows. Alternatives: run tests in GitHub Actions or a virtual machine. See [docs/testing.md](docs/testing.md).

## Permissions

All of version 0.1 runs **without elevation** (`asInvoker`). Some data about protected processes shows as "Access denied", which is expected. Details: [docs/permissions.md](docs/permissions.md).

## Privacy

Local processing, no telemetry. Data lives in `%LocalAppData%\OptimaPC` (history, logs, settings) and can be deleted from the app. Serial numbers, product keys and volume labels are not collected. Details: [docs/privacy.md](docs/privacy.md).

## Roadmap

1. **Read-only diagnostics** ← current version
2. Recommendations with transparent, evidence-based rules
3. Selectable, reversible changes (operation engine with backup, verification and restore)
4. Manual profiles and game measurement (FPS and frame times) via a maintained integration
5. Installer, code signing and release

## Downloads and signing

Releases are published as a portable `.zip`, built on GitHub Actions, signed and with a SHA-256 hash ([how to verify](docs/release.md)). Do not turn off any Windows protection to run it.

Free code signing provided by [SignPath.io](https://about.signpath.io), certificate by [SignPath Foundation](https://signpath.org). See the [code signing policy](docs/code-signing-policy.md).

## License

[MIT](LICENSE). Software is provided "as is", with no guarantee of performance improvement.
