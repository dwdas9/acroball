# Current State

Last updated: 2026-07-07

## Current Milestone
M3/M4 gap closure, discovered mid-M5: the project had jumped to M5
(packaging) with Compress (M4) and Organize (M3) still stubbed. Both are now
implemented and committed. M5's original task (verify the app-release
workflow end-to-end on GitHub) is still outstanding underneath this.

## Last Completed Action
Implemented and committed the Organize tool (M3) in commit `fd10872`:
- `PdfRenderService` (`src/Acroball.Infrastructure/Pdf/PdfRenderService.cs`)
  implements `IPdfRenderService` over PDFtoImage/PDFium, serialized behind a
  single-slot semaphore (PDFium isn't thread-safe). See
  `docs/adr/0010-render-service-pdftoimage.md` for the platform-compatibility
  attribute chain this required (`PdfRenderService` →
  `AddAcroballInfrastructure` → `DesktopComposition.BuildServiceProvider` →
  `Program.Main`/`BuildAvaloniaApp`, all need matching
  `[SupportedOSPlatform("windows"/"macos"/"linux")]`).
- `PdfSharpEngine.ComposeAsync` assembles an explicit, possibly cross-file
  page list into one output document with a per-page rotation delta.
- `ComposeJob`/`ComposeJobRequest` in `Acroball.Application/Jobs`.
- `OrganizeViewModel`/`OrganizePageViewModel`/`OrganizeView` in `Acroball.UI`:
  a page-tile grid with real PDFium thumbnails, drag-and-drop reorder (same
  DragDrop pattern as `MergeView`), per-tile rotate/delete, multi-file add
  with an inline per-file password prompt, wired into `PageFactory`,
  `ViewLocator`, and `UiServiceCollectionExtensions`.
- Full solution build (Debug + Release) and test suite are green: 143 tests
  passed, 0 failed (`Acroball.Domain.Tests` 33, `Acroball.Application.Tests`
  4, `Acroball.UI.Tests` 54, `Acroball.Infrastructure.Tests` 52).

Compress (M4) was implemented and committed earlier this session in commit
`eca042b` (see `docs/adr/0009-compress-image-recompression.md`), and a
blanket `*.md` gitignore rule that had kept `CLAUDE.md`, all ADRs, and this
file itself out of version control was fixed in commit `13f77c9`.

## Current Blockers
Organize has **not** been manually verified running inside the real desktop
app. A standalone smoke-test harness (a scratch console project outside the
repo, at `%TEMP%\claude\...\scratchpad\organizesmoke`, referencing
`Acroball.UI`/`Acroball.Infrastructure` directly with a headless Avalonia
setup) was built to drive `OrganizeViewModel` end-to-end against a real
multi-page PDF with embedded JPEGs, but the `dotnet run` invocation hung
indefinitely with zero output — even the first `Console.WriteLine` never
appeared after ~9 minutes. Root cause is unconfirmed; candidates not yet
ruled out:
- A deadlock in `PdfRenderService`'s `SemaphoreSlim` gate (unlikely — the
  integration test `Concurrent_render_calls_are_serialized_and_all_succeed`
  in `PdfRenderServiceTests.cs` exercises 8 concurrent callers against the
  same gate and passes).
- `AppBuilder.Configure<Application>().UseHeadless(...).SetupWithoutStarting()`
  hanging or blocking in this environment.
- The scratch project's first `dotnet run` needing a slow NuGet
  restore/build that simply hadn't finished within the observed window.
All 143 tests in the actual solution (which don't use Avalonia headless
setup) pass cleanly, so this is most likely specific to the throwaway
harness's setup, not the shipped `OrganizeViewModel`/`PdfRenderService` code
— but that is not yet confirmed by actually watching the app run.

## Next Immediate Task
Get an actual, watched confirmation that Organize works end-to-end in the
real running desktop app: run `dotnet run --project src/Acroball.Desktop`,
navigate to Organize (sidebar or `Ctrl+4`), add a real multi-page PDF, and
visually confirm thumbnails render, drag-to-reorder works, rotate/delete
work, and Compose produces a correct output file — take a screenshot as
evidence. If that works cleanly, the standalone smoke-test harness hang was
a scratch-project artifact and can be discarded without further
investigation. Only after this is confirmed should the session return to
the original M5 task: push the app-release workflow and verify a real
`app-v0.1.0` release on GitHub.

## Context Dependency Index
- src/Acroball.UI/ViewModels/OrganizeViewModel.cs
- src/Acroball.UI/Views/OrganizeView.axaml
- src/Acroball.UI/Views/OrganizeView.axaml.cs
- src/Acroball.Infrastructure/Pdf/PdfRenderService.cs
- src/Acroball.Infrastructure/Pdf/PdfSharpEngine.cs
- src/Acroball.Desktop/Program.cs
- docs/adr/0010-render-service-pdftoimage.md
- .github/workflows/app-release.yml
