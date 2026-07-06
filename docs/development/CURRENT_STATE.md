# Current State

Last updated: 2026-07-06

## Current Milestone
M4-UI — Finish Split, Extract, Rotate tools (user chose this over starting
the historical M4 Compress/Protect/Metadata package). Full plan on disk at
`C:\Users\dasd\.claude\plans\replicated-dazzling-pretzel.md`.

## Last Completed Action
All production code and tests for Split/Extract/Rotate have been written,
mirroring `MergeJobRequest`/`MergeJob`/`MergeViewModel`/`MergeView` exactly.
No `IPdfEngine`/Domain changes were needed (the engine support already
existed and was already tested). Files created/modified this session:

Application layer (builds clean — verified via
`dotnet build src/Acroball.Application/Acroball.Application.csproj`):
- `src/Acroball.Application/Jobs/SplitJobRequest.cs`, `SplitJob.cs`
- `src/Acroball.Application/Jobs/ExtractPagesJobRequest.cs`, `ExtractPagesJob.cs`
- `src/Acroball.Application/Jobs/RotatePagesJobRequest.cs`, `RotatePagesJob.cs`

UI layer (builds clean — verified via
`dotnet build src/Acroball.UI/Acroball.UI.csproj`, including AXAML compilation):
- `src/Acroball.UI/ViewModels/SplitViewModel.cs`, `ExtractViewModel.cs`, `RotateViewModel.cs`
- `src/Acroball.UI/Views/{Split,Extract,Rotate}View.axaml` + `.axaml.cs`
- Wiring edits: `src/Acroball.UI/DependencyInjection/UiServiceCollectionExtensions.cs`,
  `src/Acroball.UI/Services/PageFactory.cs`, `src/Acroball.UI/ViewLocator.cs`
  (3 new DI registrations / switch cases / registry entries each, mirroring the
  existing `merge` entries).

Two build issues were hit and fixed during this session (both already
resolved, noted in case similar patterns recur):
- `RotatePagesJob.cs` needed `using Acroball.Domain;` for the
  `Rotation.ToDegrees()` extension method.
- `ViewLocator.cs`'s `new SplitView()` was ambiguous with
  `Avalonia.Controls.SplitView` (a real Avalonia control) — fixed by fully
  qualifying as `new Views.SplitView()`. The three new `*View.axaml.cs` files
  also needed an explicit `using Avalonia.Platform.Storage;` (for
  `IStorageItem.TryGetLocalPath()`) that isn't implied by the other usings —
  `MergeView.axaml.cs` has this import; it was initially missed when the new
  files were drafted.

Tests written (not yet run this session):
- `tests/Acroball.UI.Tests/SplitViewModelTests.cs`, `ExtractViewModelTests.cs`, `RotateViewModelTests.cs`
  — mirror `MergeViewModelTests.cs` conventions, with an added `IPdfEngine`
  parameter on each `CreateViewModel(...)` factory (these VMs call
  `InspectAsync`, unlike Merge).

No `ToolCatalog`/`MainWindowViewModel`/Domain/`IPdfEngine` changes — none were
needed per the plan.

## Current Blockers
None yet identified, but the full solution has not been built/tested since
the three new test files were added — `dotnet test Acroball.sln` has not run
yet this session.

## Next Immediate Task
Run `dotnet test Acroball.sln` (or build first with `dotnet build Acroball.sln`
if a full-solution build hasn't been confirmed), fix any compile/test failures
in the three new test files or ViewModels, then finish the plan in
`C:\Users\dasd\.claude\plans\replicated-dazzling-pretzel.md`:
1. Get `dotnet test Acroball.sln` fully green (Domain/Application/Infrastructure/UI test projects).
2. Manual smoke test: `dotnet run --project src/Acroball.Desktop`, exercise Split, Extract, and Rotate through the real UI per the checklist in the plan file's Verification section (drag a real multi-page PDF, confirm page count reads via `InspectAsync`, confirm out-of-range page validation, confirm Rotate's default "90° clockwise" selection via the `EnumToBooleanConverter` RadioButtons, confirm output files are produced and correct when opened in a real PDF viewer, confirm Cancel/Open-Output-Folder/Reset buttons work, confirm a deliberate failure path surfaces a friendly error).
3. Report results back to the user; do not mark the milestone done until both automated tests and the manual smoke test have passed.

Do not re-decide the design — the approved plan file has full ViewModel/View/
Job specifics already implemented.

## Context Dependency Index

- C:\Users\dasd\.claude\plans\replicated-dazzling-pretzel.md
- src/Acroball.UI/ViewModels/SplitViewModel.cs
- src/Acroball.UI/ViewModels/ExtractViewModel.cs
- src/Acroball.UI/ViewModels/RotateViewModel.cs
- src/Acroball.UI/Views/SplitView.axaml.cs
- src/Acroball.UI/Views/ExtractView.axaml.cs
- src/Acroball.UI/Views/RotateView.axaml.cs
- src/Acroball.UI/ViewLocator.cs
- tests/Acroball.UI.Tests/SplitViewModelTests.cs
- tests/Acroball.UI.Tests/ExtractViewModelTests.cs
- tests/Acroball.UI.Tests/RotateViewModelTests.cs
