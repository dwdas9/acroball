# Current State

Last updated: 2026-07-07

## Current Milestone
M4 — Core PDF tools (metadata, protect, compress). Compress was found to have
been skipped earlier in favor of jumping ahead to M5 packaging; this session
went back and implemented it before M5 resumes.

## Last Completed Action
Implemented the Compress tool end-to-end (previously a stub that threw
`NotSupportedException`):
- `PdfSharpEngine.CompressAsync` in
  `src/Acroball.Infrastructure/Pdf/PdfSharpEngine.cs` now rebuilds the
  document page-by-page (drops unreferenced objects) for every profile, and
  for `Balanced`/`Aggressive` additionally recompresses qualifying embedded
  JPEG images via SkiaSharp (decode, downsample to a profile max edge,
  re-encode at a profile quality, keep only if strictly smaller). Scope and
  rationale are written up in `docs/adr/0009-compress-image-recompression.md`.
- Added `SkiaSharp` as an explicit package (was already a transitive Avalonia
  dependency, MIT-licensed) in `Directory.Packages.props` and
  `src/Acroball.Infrastructure/Acroball.Infrastructure.csproj`.
- Added `CompressJobRequest`/`CompressJob` in `Acroball.Application/Jobs`,
  `CompressViewModel`/`CompressView` in `Acroball.UI`, and wired them into
  `PageFactory`, `ViewLocator`, and `UiServiceCollectionExtensions` so
  "Compress" in the sidebar is a real tool instead of a milestone placeholder.
- Added integration tests in
  `tests/Acroball.Infrastructure.Tests/PdfSharpEngineTests.cs` (lossless
  leaves images untouched, balanced downsamples oversized RGB JPEGs and
  shrinks the file, aggressive is smaller-or-equal to balanced, non-JPEG
  images pass through untouched, progress is monotonic) and
  `tests/Acroball.UI.Tests/CompressViewModelTests.cs`. Full solution build and
  test run (Debug and Release) are green: 122 tests passed, 0 failed.

All changes from this session are uncommitted in the working tree.

## Current Blockers
None. Solution builds clean in Debug and Release; all tests pass.

## Next Immediate Task
Decide with the user how to proceed: either (a) commit this Compress work,
then implement the other tool that was skipped earlier — **Organize** (M3,
still a `ToolPlaceholderViewModel` stub with no `ComposeAsync` engine
implementation, no Job, no ViewModel) — or (b) return to the M5 task that was
in progress before this detour: push the already-committed
`.github/workflows/app-release.yml` changes and run a real `app-v0.1.0` tag
or workflow-dispatch release to verify the GitHub Releases artifacts. Do not
assume which without asking; the user paused mid-decision, not mid-task.

## Context Dependency Index
- src/Acroball.Infrastructure/Pdf/PdfSharpEngine.cs
- src/Acroball.UI/Tools/ToolCatalog.cs
- src/Acroball.UI/Services/PageFactory.cs
- src/Acroball.Application/Abstractions/IPdfEngine.cs
- src/Acroball.Application/Operations/Requests.cs
- docs/adr/0009-compress-image-recompression.md
- .github/workflows/app-release.yml
- README.md
