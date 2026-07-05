# Acroball

**Everything your PDFs need. Fast, private and fully offline.**

Built by dwdas as a personal, modern PDF tool for desktop use.

Acroball is a free, open-source, cross-platform desktop app for working with PDF
files â€” merge, split, organize, rotate, extract, compress, protect and edit
metadata â€” built as a modern successor to tools like PDFsam Basic. No
accounts, no uploads, no telemetry: your documents never leave your machine.

Built with .NET 10 and Avalonia 12. Runs natively on Windows, macOS and Linux.

## Status


| --- | --- |
| Merge | Milestone 2 |
| Split | Milestone 2 |
| Rotate | Milestone 2 |
| Extract pages | Milestone 2 |
| Organize (visual page editor) | Milestone 3 |
| Compress | Milestone 4 |
| Protect (encrypt/decrypt) | Milestone 4 |
| Metadata editor | Milestone 4 |
| Plugins | Milestone 5 |
| Installers & self-update | Milestone 6 |

## Building

ReAcroballs the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0).

```bash
dotnet build
dotnet run --project src/Acroball.Desktop
dotnet test
```

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

