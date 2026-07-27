# Current State

Last updated: 2026-07-28

## Current Milestone
M5/M6/M7 — Acrobat-style viewing and editing (Viewer, Bookmarks, Annotations,
Fill Form) plus the release pipeline — all closed out. `main` now contains
everything from `feature/pdf-viewer-editing` (merged via PR #1 plus one
follow-up merge commit for a null-safety fix that landed after the PR).
No milestone is currently active; the next one has not yet been chosen.

## Last Completed Action
Verified and closed the M5 release task: pushed tag `app-v0.1.0` on `main`,
which ran the `App Release` GitHub Actions workflow end-to-end
(`gh run list`/`gh run watch` confirmed `completed`/`success` in 2m6s) and
published a real GitHub release (`gh release view app-v0.1.0`) with all five
platform assets — `acroball-win-x64.zip`, `acroball-osx-x64.tar.gz`,
`acroball-osx-arm64.tar.gz`, `acroball-linux-x64.tar.gz`,
`acroball-linux-arm64.tar.gz`. Before that, merged `feature/pdf-viewer-editing`
into `main` (`d4f6b0b`) after discovering PR #1 had merged the branch one
commit short of its tip (missing the `AnnotationPreviewShape.Points`
null-safety fix); local `main` was synced to `origin/main` first, then the
remaining commit was merged in with `--no-ff`, rebuilt, and retested (204
tests, 0 failures) before pushing.

## Current Blockers
None. `main` is clean, up to date with `origin/main`, builds, and passes the
full test suite (204 tests). `feature/pdf-viewer-editing` is fully merged and
can be deleted whenever convenient (not yet done, no urgency).

## Next Immediate Task
Get the next milestone's scope from the user, then begin it. No task is
implied by the repository alone right now — this is a genuine decision
point, not something to infer from code or commit history.

## Context Dependency Index
None.
