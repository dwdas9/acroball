# Acroball

**Everything your PDFs need. Fast, private and fully offline.**

Built by dwdas as a personal, modern PDF tool for desktop use.

Acroball is a free, open-source, cross-platform desktop app for working with PDF
files — view, annotate, fill forms, merge, split, organize, rotate, extract,
compress, protect and edit metadata — built as a modern successor to tools like PDFsam Basic. No
accounts, no uploads, no telemetry: your documents never leave your machine.

Built with .NET 10 and Avalonia 12. Runs natively on Windows, macOS and Linux.

## Status

**1.0.** All ten tools are complete. What 1.0 includes, what is deferred to
1.x (self-update, plugins, code signing) and the release criteria are in
[ADR-0015](docs/adr/0015-v1-scope-and-release-criteria.md).

Builds are unsigned, so Windows SmartScreen and macOS Gatekeeper warn on
first launch. On Windows choose *More info → Run anyway*; on macOS
right-click the app and choose *Open*.

## Building

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0).

```bash
dotnet build
dotnet run --project src/Acroball.Desktop
dotnet test
```

## Download executables

Acroball executables are published separately from normal source/CI runs.

App releases produce prebuilt binaries for:

- Windows (`win-x64`)
- macOS (`osx-x64`, `osx-arm64`)
- Linux (`linux-x64`, `linux-arm64`)

How to get them:

1. Open the Releases page in this repository.
2. Choose the app release for your version.
3. Download the archive matching your platform.
4. Extract and run the executable.

App releases are created by the dedicated `App Release` workflow, not by the
normal `CI` workflow.

If you are building locally on Windows, the generated executable path is:

```text
src/Acroball.Desktop/bin/Release/net10.0/win-x64/publish/Acroball.exe
```

## Milestones

See the milestones table in [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md).

## Project layout

```
src/Acroball.Domain          entities, no dependencies
src/Acroball.Application     use cases, jobs, engine abstractions
src/Acroball.Infrastructure  PDFsharp/PDFium engines, settings, logging
src/Acroball.UI              Avalonia views and view models
src/Acroball.Desktop         composition root and app host
src/Acroball.Sdk             plugin contract (reserved for 1.x)
tests/                       one test project per layer
```

See [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) for the full picture and
[docs/adr/](docs/adr/) for why things are the way they are.

## License

[MIT](LICENSE). Third-party attributions in
[THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).

© 2026 dwdas. All rights reserved.

