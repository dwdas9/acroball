# Development Guide

This guide is the starting point for engineers working on Acroball.

## Prerequisites

- .NET SDK 10.0.301 or a compatible later feature band.
- A platform supported by Avalonia Desktop: Windows, macOS or Linux.
- Git.

The SDK is pinned in [../../global.json](../../global.json). Package versions
are centrally managed in [../../Directory.Packages.props](../../Directory.Packages.props).

## Common Commands

```bash
dotnet restore
dotnet build
dotnet run --project src/Acroball.Desktop
dotnet test
```

The current audited test command was:

```bash
dotnet test --no-restore
```

It passed 75 tests on 2026-07-06.

## Solution Layout

```text
src/
  Acroball.Domain          Pure domain model and PDF value types
  Acroball.Application     Application abstractions, request records and jobs
  Acroball.Infrastructure  Persistence, logging, update stub and PDFsharp engine
  Acroball.UI              Avalonia views, view models, services and theme
  Acroball.Desktop         Entry point and composition root
  Acroball.Sdk             Plugin contract assembly

tests/
  Acroball.Domain.Tests
  Acroball.Application.Tests
  Acroball.Infrastructure.Tests
  Acroball.UI.Tests

docs/
  ARCHITECTURE.md
  adr/
  development/
  releases/
  user/
```

## Architecture Rules

- Dependencies point inward. UI depends on Application, not Infrastructure.
- Desktop is the only composition root and references both UI and Infrastructure.
- Domain has no package dependencies.
- Infrastructure owns concrete persistence, logging, update and PDF services.
- UI view models talk to Application abstractions instead of backend types.
- View resolution is explicit through `ViewLocator`.
- Service registration is layer-specific and composed in
  `Acroball.Desktop/Composition/DesktopComposition.cs`.

Current implementation note: Application now contains the shared job framework
and merge job orchestration. `MergeJobRequest.Validate()` performs filesystem
validation. Treat this as an intentional current-state detail, but review it
before repeating the pattern broadly.

## Dependency Injection

- Infrastructure services are registered by `AddAcroballInfrastructure`.
- UI services and view models are registered by `AddAcroballUi`.
- The final `ServiceProvider` is built with `ValidateOnBuild` and
  `ValidateScopes`.

## Runtime Data

`AppPaths` resolves the per-user data directory and creates:

- `settings.json`
- `recent.json`
- `logs/Acroball-yyyyMMdd.log`

The `Acroball_DATA_DIR` environment variable overrides the default data
directory for portable installs and hermetic tests.

## Testing

- xUnit v3 is used across all test projects.
- Test projects are executables.
- Infrastructure tests use real temporary files.
- PDF engine tests generate fixture PDFs with PDFsharp.
- UI tests currently exercise `MergeViewModel` without Avalonia Headless.

Run tests before changing documentation that describes build or test status.

## Documentation Workflow

Update documentation as part of each milestone:

- [PROJECT_STATUS.md](PROJECT_STATUS.md)
- [ROADMAP.md](ROADMAP.md)
- [CHANGELOG.md](CHANGELOG.md)
- [IMPLEMENTATION_HISTORY.md](IMPLEMENTATION_HISTORY.md)
- [DECISION_LOG.md](DECISION_LOG.md)
- the relevant file in [../releases/](../releases/)

Use [DEVELOPMENT_SESSION.md](DEVELOPMENT_SESSION.md) for temporary active
handover notes. Fold durable information into permanent documents at the end
of a milestone, then refresh the session document.
