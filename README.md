# Acroball

**Everything your PDFs need. Fast, private and fully offline.**

Built by dwdas as a personal, modern PDF tool for desktop use.

Acroball is a free, open-source, cross-platform desktop app for working with PDF
files â€” merge, split, organize, rotate, extract, compress, protect and edit
metadata â€” built as a modern successor to tools like PDFsam Basic. No
accounts, no uploads, no telemetry: your documents never leave your machine.

Built with .NET 10 and Avalonia 12. Runs natively on Windows, macOS and Linux.

## Status

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

- M4: Core PDF tools (metadata, protect, compress)
- M5: Packaging and distribution (publish executables/installers for Windows, macOS, Linux)

## Project layout

```
TBD
```

See [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) for the full picture and
[docs/adr/](docs/adr/) for why things are the way they are.

## License

[MIT](LICENSE). Third-party attributions in
[THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).

© 2026 dwdas. All rights reserved.

