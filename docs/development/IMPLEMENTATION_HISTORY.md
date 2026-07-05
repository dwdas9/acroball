# Implementation History

This is the append-only engineering journal for Acroball.

Dates are taken from git history where available. The first repository commit
contains multiple implementation milestones, so M1 through M3 are reconstructed
from the current implementation and existing documentation rather than from
separate milestone commits.

## 2026-07-06 - M1 - Application Shell

### Summary

Established the initial .NET 10 solution, Clean Architecture project layout,
Avalonia shell, design system, settings persistence, logging and test harness.

### Architecture

- Introduced Domain, Application, Infrastructure, UI, Desktop and SDK projects.
- Kept Desktop as the composition root.
- Introduced explicit view location for trim-safe navigation.
- Added semantic theme resources and control styles.

### Files Added

- `Acroball.sln`
- `global.json`
- `Directory.Build.props`
- `Directory.Packages.props`
- `src/Acroball.Domain/`
- `src/Acroball.Application/`
- `src/Acroball.Infrastructure/`
- `src/Acroball.UI/`
- `src/Acroball.Desktop/`
- `src/Acroball.Sdk/`
- `tests/*`
- `docs/ARCHITECTURE.md`
- `docs/adr/*`

### Files Modified

- Not applicable; this was reconstructed from the initial repository commit.

### Tests

- Domain, infrastructure and UI test projects were added as part of the
  initial tracked codebase.

### Lessons Learned

- The architecture was intentionally designed for long-term growth, including
  plugins, trimming and cross-platform packaging.
- UI polish and engineering documentation were treated as first-class work
  from the beginning.

### Release Document

See [../releases/M1.md](../releases/M1.md).

## 2026-07-06 - M2 - PDF Manipulation Engine

### Summary

Implemented the PDFsharp-backed manipulation engine and operation contracts.

### Architecture

- Added `IPdfEngine` in Application.
- Kept PDFsharp isolated in Infrastructure.
- Added request records for merge, split, extract, rotate, compose, encrypt,
  decrypt, compress and metadata operations.
- Added atomic output writes and progress reporting.

### Files Added

- `src/Acroball.Application/Abstractions/IPdfEngine.cs`
- `src/Acroball.Application/Operations/Requests.cs`
- `src/Acroball.Application/Operations/OperationProgress.cs`
- `src/Acroball.Infrastructure/Pdf/PdfSharpEngine.cs`
- `src/Acroball.Infrastructure/Pdf/OutputNameTemplate.cs`
- `tests/Acroball.Infrastructure.Tests/PdfSharpEngineTests.cs`
- `tests/Acroball.Infrastructure.Tests/OutputNameTemplateTests.cs`

### Files Modified

- `Directory.Packages.props`
- `src/Acroball.Infrastructure/Acroball.Infrastructure.csproj`
- `src/Acroball.Infrastructure/DependencyInjection/InfrastructureServiceCollectionExtensions.cs`

### Tests

- PDF engine integration tests generate real PDF fixtures with PDFsharp.
- Current audited result includes 31 passing Infrastructure tests.

### Lessons Learned

- Geometry-based PDF fixture assertions avoid font dependencies in CI.
- PDFsharp is sufficient for core manipulation workflows but not rendering.

### Release Document

See [../releases/M2.md](../releases/M2.md).

## 2026-07-06 - M3 - Merge Workflow

### Summary

Implemented the first full user-facing PDF workflow: Merge PDFs.

### Architecture

- Added a shared `IJobExecutor` pipeline and `JobRunner`.
- Added `MergeJobRequest` and `MergeJob`.
- Connected the merge UI to `IPdfEngine` through Application abstractions.
- Added `IFileDialogService` so the ViewModel can remain testable.

### Files Added

- `src/Acroball.Application/Abstractions/IJobExecutor.cs`
- `src/Acroball.Application/Jobs/*`
- `src/Acroball.UI/ViewModels/MergeViewModel.cs`
- `src/Acroball.UI/Views/MergeView.axaml`
- `src/Acroball.UI/Views/MergeView.axaml.cs`
- `src/Acroball.UI/Services/IFileDialogService.cs`
- `src/Acroball.UI/Services/AvaloniaFileDialogService.cs`
- `tests/Acroball.Application.Tests/JobRunnerTests.cs`
- `tests/Acroball.UI.Tests/MergeViewModelTests.cs`

### Files Modified

- `src/Acroball.UI/Services/PageFactory.cs`
- `src/Acroball.UI/ViewLocator.cs`
- `src/Acroball.UI/DependencyInjection/UiServiceCollectionExtensions.cs`
- `src/Acroball.UI/Tools/ToolCatalog.cs`

### Tests

- Current audited result includes 4 passing Application tests and 7 passing
  UI tests for the merge workflow.

### Lessons Learned

- The job pipeline gives the UI a consistent place for validation, progress,
  cancellation and error translation.
- ViewModel-level tests provide good coverage for workflow behavior, but a
  future rendered UI smoke test harness is still needed.

### Release Document

See [../releases/M3.md](../releases/M3.md).

## 2026-07-06 - Documentation Foundation

### Summary

Established the professional documentation framework requested for Acroball.

### Architecture

- Preserved existing architecture and ADR documents.
- Added current-state documentation for development, status, roadmap,
  decisions, standards, releases and user guidance.
- Added a temporary development session document distinct from permanent
  implementation history.

### Files Added

- `docs/architecture/README.md`
- `docs/development/DEVELOPMENT.md`
- `docs/development/PROJECT_STATUS.md`
- `docs/development/ROADMAP.md`
- `docs/development/CHANGELOG.md`
- `docs/development/IMPLEMENTATION_HISTORY.md`
- `docs/development/DECISION_LOG.md`
- `docs/development/CODING_STANDARDS.md`
- `docs/development/CONTRIBUTING.md`
- `docs/development/RELEASE_CHECKLIST.md`
- `docs/development/DEVELOPMENT_SESSION.md`
- `docs/releases/M1.md`
- `docs/releases/M2.md`
- `docs/releases/M3.md`
- `docs/releases/TEMPLATE.md`
- `docs/user/USER_GUIDE.md`
- `docs/user/FAQ.md`

### Files Modified

- `README.md`
- `THIRD-PARTY-NOTICES.md`
- `docs/ARCHITECTURE.md`
- selected ADRs with current-state notes where necessary.

### Tests

- `dotnet test --no-restore` passed 75 tests.

### Lessons Learned

- The implementation is ahead of the permanent documentation.
- Some older milestone language conflicts with the current M3 Merge Workflow
  scope and should not guide new feature work without clarification.

### Release Document

No separate release document is created for the Documentation Foundation
unless the project later treats it as a numbered milestone.
