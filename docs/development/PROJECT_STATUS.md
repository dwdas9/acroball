# Project Status

Last updated: 2026-07-06

## Current Version

0.1.0

## Current Milestone

Documentation Foundation after M3 Merge Workflow implementation.

No production feature work is in progress in this milestone.

## Architecture Summary

Acroball is a .NET 10 and Avalonia 12 desktop application using Clean
Architecture and MVVM.

- `Acroball.Domain`: PDF value types, page ranges, rotations, permissions,
  metadata and PDF exceptions.
- `Acroball.Application`: service abstractions, operation request records,
  progress records and the shared job execution framework.
- `Acroball.Infrastructure`: JSON persistence, recent files, rolling file
  logging, update stub and `PdfSharpEngine`.
- `Acroball.UI`: Avalonia views, view models, theme resources, file dialogs,
  navigation and the merge workflow UI.
- `Acroball.Desktop`: application entry point and composition root.
- `Acroball.Sdk`: plugin contract surface for a future plugin milestone.

The full architecture overview is in [../ARCHITECTURE.md](../ARCHITECTURE.md).

## Completed Milestones

| Milestone | Status | Evidence |
| --- | --- | --- |
| M1 - Application Shell | Complete | Shell, navigation, design system, settings, logging, tests |
| M2 - PDF Manipulation Engine | Complete at engine level | PDFsharp engine, operation records and integration tests |
| M3 - Merge Workflow | Complete at current scope | Merge view, view model, job framework and UI tests |

## Implemented Features

- Cross-platform Avalonia desktop shell.
- Home page with tool catalog.
- Sidebar navigation and keyboard shortcuts `Ctrl+1` through `Ctrl+9`.
- Settings page with System, Light and Dark theme selection.
- Persisted window size and maximized state.
- JSON settings persistence with atomic writes.
- Recent files service with atomic writes and capped history.
- Rolling file logger with 14-day retention.
- Null update service for pre-packaging builds.
- PDF inspection and page geometry through PDFsharp.
- PDF merge, split, extract, rotate and metadata update at engine level.
- Merge workflow UI with file picking, drag/drop add, drag/drop reorder,
  validation, progress, cancellation and result state.

## Features In Progress

- Documentation Foundation.

## Planned Features

- Split, Extract and Rotate tool pages.
- Password handling in user-facing workflows.
- PDF rendering service implementation behind `IPdfRenderService`.
- Visual Organize workflow using page thumbnails.
- Compose support in `IPdfEngine`.
- Compress, Protect and Metadata editor workflows.
- Command palette.
- Custom window chrome.
- Plugin loading and plugin manager.
- Accessibility pass.
- Velopack packaging and self-update.

## Current Dependencies

Primary runtime and UI dependencies:

- Avalonia 12.0.5
- Avalonia.Desktop 12.0.5
- Avalonia.Themes.Fluent 12.0.5
- Avalonia.Fonts.Inter 12.0.5
- CommunityToolkit.Mvvm 8.4.0
- Microsoft.Extensions.DependencyInjection 10.0.0
- Microsoft.Extensions.Logging 10.0.0
- PDFsharp 6.2.4

Test dependencies:

- xunit.v3 1.0.0
- xunit.runner.visualstudio 3.0.0
- Microsoft.NET.Test.Sdk 17.12.0
- Moq 4.20.72

## Current Test Status

Audited on 2026-07-06 with:

```bash
dotnet test --no-restore
```

Result: passed.

| Test Project | Passed |
| --- | ---: |
| Acroball.Domain.Tests | 33 |
| Acroball.Application.Tests | 4 |
| Acroball.Infrastructure.Tests | 31 |
| Acroball.UI.Tests | 7 |
| Total | 75 |

The first sandboxed run failed with an access-denied write to `obj/`; the
unsandboxed run passed and is the recorded test status.

## Known Issues

- Existing documentation contained stale milestone descriptions and encoding
  artifacts before this foundation update.
- `README.md` had an empty status section and placeholder project layout.
- Several source comments and display strings still contain mojibake such as
  `â`.
- `src/Acroball.Sdk/IQuirePlugin.cs` and
  `src/Acroball.Infrastructure/Persistence/QuireJsonContext.cs` retain old
  `Quire` names even though the public types are Acroball types.
- Ignored `bin/`, `obj/` and `.vs/` folders exist locally and include older
  `Quire.*` artifacts.
- `error.txt` and `output.txt` are tracked but empty.
- `IPdfRenderService` has a contract but no registered implementation.
- Split, Extract and Rotate are implemented in the engine but still have
  placeholder UI pages.
- No Avalonia Headless test harness is currently present.

## Known Technical Debt

- Application-layer job validation currently uses filesystem checks.
- Merge UI does not expose password input for encrypted PDFs.
- Merge workflow has ViewModel tests but no rendered UI smoke tests.
- Documentation and source comments should be normalized to UTF-8 text.
- Milestone naming should be stabilized; older docs described M3 as visual
  organization, while the current prompt and implementation define M3 as
  Merge Workflow.

## Recommended Next Milestone

Before starting feature work, clarify whether the next milestone should:

1. Complete the remaining basic tool pages using the existing engine support
   for Split, Extract and Rotate, or
2. Resume the historical roadmap with rendering and visual organization, or
3. Begin the M4 Compress, Protect and Metadata package.

Do not begin M4 implementation until that scope is explicit.
