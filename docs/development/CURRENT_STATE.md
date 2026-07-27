# Current State

Last updated: 2026-07-27

## Current Milestone
M6/M7 — Acrobat-style viewing and editing (Viewer, Bookmarks, Annotations,
Fill Form), on branch `feature/pdf-viewer-editing` (up to date with
`origin/feature/pdf-viewer-editing`, not yet merged into `main`).

## Last Completed Action
Manually, interactively verified all four M6/M7 features in the real running
desktop app (the blocker recorded at the end of the last session): Viewer's
continuous-scroll rendering and bookmark navigation, drawing and saving all
four annotation kinds (Highlight/FreeText/Ink/Square) with a confirmed
round-trip on reopen, and Fill Form's field rendering/editing/save round-trip
— the user ran this pass themselves and confirmed all of it works end-to-end.

During this pass, one launch of the app crashed on startup with
`System.ArgumentNullException: Value cannot be null. (Parameter 'collection')`
inside Avalonia's `Polyline.CreateDefiningGeometry()`. Root-caused to
`AnnotationPreviewShape.Points` (`src/Acroball.UI/ViewModels/AnnotationPreviewShape.cs`)
being a nullable `Points?` defaulting to `null` for every non-Ink shape, while
`ViewerView.axaml` binds it directly into a `Polyline.Points` — a real
null-safety gap, though it was not conclusively reproduced as the exact cause
of that one crash (a subsequent relaunch of the pre-fix build ran clean for
the rest of the session). Fixed by defaulting `Points` to `[]` instead of
`null`, committed in `39f790f` along with the checkpoint update that
introduced this file's prior revision. Full solution build and the full test
suite (204 tests: `Acroball.Domain.Tests` 33, `Acroball.Application.Tests` 4,
`Acroball.UI.Tests` 99, `Acroball.Infrastructure.Tests` 68) are green with
the fix in place.

## Current Blockers
1. Branch `feature/pdf-viewer-editing` is not yet merged into `main`; the
   user has decided to merge as-is (merge commit, preserving branch
   history) now that all four M6/M7 features are manually confirmed working.
2. Pre-existing, unrelated to this feature work: the M5 task (push the
   already-committed `.github/workflows/app-release.yml` and verify a real
   `app-v0.1.0` release on GitHub) is still outstanding underneath
   everything above.

## Next Immediate Task
Merge `feature/pdf-viewer-editing` into `main` (merge commit, not squash),
then resume the outstanding M5 task (push `app-release.yml`, verify the
`app-v0.1.0` GitHub release).

## Context Dependency Index
- src/Acroball.UI/ViewModels/AnnotationPreviewShape.cs
- src/Acroball.UI/Views/ViewerView.axaml
- .github/workflows/app-release.yml
