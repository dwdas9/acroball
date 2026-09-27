# Current State

Last updated: 2026-09-27

## Current Milestone
1.0 release, as defined by ADR-0015 (scope, deferrals, release criteria).

## Last Completed Action
Release criteria 1–3 of ADR-0015 are met and verified locally on Windows.
`Directory.Build.props` is `<Version>1.0.0</Version>`; Release build has 0
warnings/0 errors and all 207 tests pass. `.github/workflows/app-release.yml`
now publishes `win-x64` as a folder build (not single-file) and strips
`.pdb`/`.xml` from every archive. A local package
`dist/acroball-1.0.0-win-x64.zip` (gitignored) was extracted fresh and
launched to a visible "Acroball" window. Double-encoded text was repaired in
10 files, guarded by `tests/Acroball.UI.Tests/ToolCatalogTests.cs`. Work is
committed on local branch `release/1.0`, not pushed.

## Current Blockers
Criterion 4 (the `app-v1.0.0` GitHub release) needs the user's go-ahead to
push, because pushing the tag publishes a public release. The macOS/Linux
archives and the workflow edit are unverified until that run.

## Next Immediate Task
With user approval: merge `release/1.0` into `main`, push, tag
`app-v1.0.0`, push the tag, and confirm the `App Release` run attaches all
five platform archives.

## Context Dependency Index
- docs/adr/0015-v1-scope-and-release-criteria.md
- .github/workflows/app-release.yml
