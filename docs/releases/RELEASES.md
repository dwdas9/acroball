# Releases

One compact entry per shipped milestone — what shipped, and the handful of
files that anchor it. Rationale lives in ADRs (`docs/adr/`); current shape
lives in `../ARCHITECTURE.md`. This file is a shipped-features log, not a
place for status, lessons-learned essays, or per-milestone handover docs.

## M1 — Application Shell

.NET 10 solution, Clean Architecture layout, Avalonia shell (home, sidebar
nav, `Ctrl+1`…`9`), Light/Dark/System theme, persisted window state, JSON
settings + recent files with atomic writes, rolling file logger, null update
service, baseline test projects.

Key files: `src/Acroball.Desktop/Composition/DesktopComposition.cs`,
`src/Acroball.UI/ViewLocator.cs`, `src/Acroball.UI/Theme/*`,
`src/Acroball.Infrastructure/Persistence/*`.

## M2 — PDF Manipulation Engine

`IPdfEngine` (Application) implemented by `PdfSharpEngine` (Infrastructure):
inspect, merge, split, extract, rotate, metadata update; atomic output
writes; progress and cancellation. Compose/Encrypt/Decrypt/Compress throw
`NotSupportedException` until M3/M4.

Key files: `src/Acroball.Application/Abstractions/IPdfEngine.cs`,
`src/Acroball.Infrastructure/Pdf/PdfSharpEngine.cs`.

## M3 — Merge Workflow

First full user-facing PDF workflow. Shared job framework (`IJobExecutor`,
`JobRunner`, `MergeJobRequest`/`MergeJob`); Merge tool page with file picker,
drag/drop add and reorder, output naming, progress, cancellation, result
state.

Key files: `src/Acroball.UI/ViewModels/MergeViewModel.cs`,
`src/Acroball.UI/Views/MergeView.axaml`, `src/Acroball.Application/Jobs/*`.

---

Add one entry like the above per milestone when it ships. Do not create a new
file per milestone. Do not restate build/test status here — that belongs in
`docs/development/CURRENT_STATE.md` while the work is active.
