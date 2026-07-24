# Current State

Last updated: 2026-07-24

## Current Milestone
M6/M7 — Acrobat-style viewing and editing (Viewer, Bookmarks, Annotations,
Fill Form), built on branch `feature/pdf-viewer-editing` off `main`. All four
features are implemented, tested, and committed on the branch; the branch
has not been merged into `main` or pushed to origin.

## Last Completed Action
Implemented and committed all four planned features, one commit each, in
this order:
- `d66f72d` — **Viewer** (M6): `ViewerViewModel`/`ViewerPageViewModel`/`ViewerView`,
  a continuous virtualized page scroll (`ListBox` + `VirtualizingStackPanel`,
  Avalonia's first use of one) rendering through the existing
  `IPdfRenderService`. See `docs/adr/0011-viewer-virtualization.md`.
- `9f73f6c` — **Bookmarks** (M6): `IPdfEngine.GetOutlineAsync` over the fully
  public `PdfSharp.Pdf.PdfOutline` API, folded into `ViewerViewModel` as a
  panel (no separate tool). See `docs/adr/0012-outline-navigation.md`.
- `d818537` — **Annotations** (M6): Highlight/FreeText/Ink/Square, hand-authored
  via low-level `PdfDictionary`/`PdfArray` object construction since PDFsharp
  6.2.4 has no public class for any of the four (confirmed by direct
  inspection of the pinned package). Also fixed `PdfRenderService` to pass
  `WithAnnotations: true` (it never had, for anything, before this — a
  latent gap this phase's spike surfaced). See
  `docs/adr/0013-hand-authored-annotations.md`.
- `037e1f2` — **Fill Form** (M7): `IPdfEngine.GetFormFieldsAsync`/`FillFormAsync`
  over `PdfSharp.Pdf.AcroForms`'s typed field API, its own new tool (not
  folded into Viewer). Required a new `SkiaSystemFontResolver` — PDFsharp 6.x
  ships no default `IFontResolver`, and merely *reading* a `PdfTextField`
  (not drawing one) makes PDFsharp eagerly construct a font and throw
  without one, on every platform, for any PDF with a text field. See
  `docs/adr/0014-acroform-field-filling.md`.

Full solution build (Debug + Release) and test suite are green after every
commit; as of the last one, 204 tests passed, 0 failed (`Acroball.Domain.Tests`
33, `Acroball.Application.Tests` 4, `Acroball.UI.Tests` 99,
`Acroball.Infrastructure.Tests` 68).

## Current Blockers
1. **None of the four new features have been manually, visually verified
   running in the real desktop app.** An attempt to drive the app via
   desktop-wide screen-coordinate automation (mouse/keyboard simulation +
   screenshotting) was made mid-session and abandoned: window-tracking was
   unreliable (coordinate mismatches, a minimized-window mixup) and it once
   captured a screenshot of an unrelated window on the shared desktop before
   the mistake was caught — that screenshot was deleted immediately, never
   read for content, and the automation approach was stopped rather than
   retried. The user chose to verify manually themselves rather than have
   automation continue. All engine-layer behavior (rendering, save/reopen
   round-trips, real pixel-level rendering assertions for every annotation
   kind) is covered by the automated test suite; only the interactive
   Avalonia UI itself (drawing gestures, toolbar, virtualized scroll,
   password dialogs, TreeView bookmark clicks) is unverified by a human.
2. Branch `feature/pdf-viewer-editing` (4 commits ahead of the `main` branch
   tip, itself synced to `fd10872`/M3 Organize) is not merged or pushed.
3. Pre-existing, unrelated to this session's work: the M5 task (push the
   already-committed `.github/workflows/app-release.yml` and verify a real
   `app-v0.1.0` release on GitHub) is still outstanding underneath
   everything above — untouched since before this session.

## Next Immediate Task
Get a watched, manual confirmation that all four features work end-to-end
in the real running desktop app: run `dotnet run --project src/Acroball.Desktop`,
then (a) open a real multi-page PDF via Viewer (sidebar or `Ctrl+0`) and
confirm continuous-scroll rendering, and bookmark navigation if the test
file has an outline; (b) draw one annotation of each kind
(Highlight/FreeText/Ink/Square) on an open page, click Save Annotations, and
reopen the saved output to confirm the annotations are actually visible;
(c) open a PDF with real AcroForm fields via Fill Form (sidebar), confirm
the field list renders correctly per kind (text box/checkbox/dropdown), edit
values, click Save Filled Form, and confirm the saved output shows the
filled values. If all of that holds up, decide with the user whether to
merge `feature/pdf-viewer-editing` into `main` (and whether to push it),
then return to the outstanding M5 task described above.

## Context Dependency Index
- src/Acroball.UI/ViewModels/ViewerViewModel.cs
- src/Acroball.UI/Views/ViewerView.axaml
- src/Acroball.UI/Views/ViewerView.axaml.cs
- src/Acroball.UI/ViewModels/FormViewModel.cs
- src/Acroball.UI/Views/FormView.axaml
- src/Acroball.UI/Views/FormView.axaml.cs
- src/Acroball.Infrastructure/Pdf/PdfSharpEngine.cs
- src/Acroball.Infrastructure/Pdf/PdfRenderService.cs
- src/Acroball.Infrastructure/Pdf/SkiaSystemFontResolver.cs
- src/Acroball.Desktop/Program.cs
- docs/adr/0011-viewer-virtualization.md
- docs/adr/0012-outline-navigation.md
- docs/adr/0013-hand-authored-annotations.md
- docs/adr/0014-acroform-field-filling.md
- .github/workflows/app-release.yml
