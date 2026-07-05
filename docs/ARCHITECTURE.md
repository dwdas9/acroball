# Acroball — Architecture

This document is the working map of the codebase. Decisions and their
rationale live in [docs/adr/](adr/); this file describes the shape that
results from them. Written for contributors and for future sessions of the
project itself.

## What Acroball is

A free, open-source, cross-platform (Windows/macOS/Linux) desktop app for
everyday PDF work: merge, split, organize, rotate, extract, compress,
protect, metadata. Positioning: the tool people reach for after PDFsam Basic
— entirely offline, no accounts, no telemetry, with a UI that feels like a
2026 product rather than a Java-era one.

Stack: **.NET 10**, **Avalonia 12**, **CommunityToolkit.Mvvm 8.4**,
**Microsoft.Extensions.DependencyInjection/Logging**. PDF backends:
**PDFsharp** (manipulation, M2) and **PDFium via PDFtoImage** (rendering,
M3), both MIT/permissive.

## Layers (ADR-0001)

```
Acroball.Desktop ──────────────► composition root; only assembly seeing everything
   │                │
Acroball.UI      Acroball.Infrastructure
   │                │
   └──► Acroball.Application ◄──┘        contracts + request records
                │
          Acroball.Domain                pure model, zero dependencies

Acroball.Sdk (plugin contract; frozen dependency surface)
```

Rules, enforced by project references:

1. Dependencies point inward only. UI never references Infrastructure.
2. Domain and Application contain no framework or I/O types — they compile
   and unit-test anywhere, including machines with no Avalonia or native
   PDF libraries.
3. Concrete services are registered in exactly one place per layer
   (`AddAcroballInfrastructure`, `AddAcroballUi`), composed only in
   `Acroball.Desktop/Composition/DesktopComposition.cs`, with
   `ValidateOnBuild` so a mis-registration fails at startup, not on click.

## The two PDF abstractions (ADR-0002)

- **`IPdfEngine`** (Application): merge/split/extract/rotate/compose/
  encrypt/decrypt/compress/metadata plus inspection. Implemented by
  `PdfSharpEngine` (Infrastructure/Pdf) since M2: PDFsharp's synchronous API
  wrapped in `Task.Run`, cancellation checked between pages, fractional
  progress per page/range, outputs written atomically (`*.tmp` + move) so a
  failed or cancelled run never leaves a truncated PDF at the destination.
  Compose/Encrypt/Decrypt/Compress throw `NotSupportedException` until
  M3/M4. Every operation takes an optional `IProgress<OperationProgress>`
  and a `CancellationToken`; failures surface as `PdfOperationException`
  subtypes (`InvalidPdfPasswordException`, `CorruptPdfException`).
- **`IPdfRenderService`** (Application): rasterizes pages to PNG bytes.
  Implemented over PDFium in M3. **PDFium is not thread-safe**: the
  implementation owns an internal sequential queue; callers may issue
  requests freely but must not assume parallel rendering.

`ComposeRequest` (an ordered list of `PageAssignment` records spanning any
number of source files) is the primitive behind the M3 visual organizer;
reorder/delete/duplicate/move all reduce to it.

## Runtime composition

`Program.Main` builds the container, wires crash logging
(AppDomain + unobserved task exceptions → log file), then starts Avalonia.
`App.OnFrameworkInitializationCompleted` reads settings, applies the theme
*before* the first frame, creates `MainWindow` with restored size/state, and
assigns the shell view model.

Navigation: the shell (`MainWindowViewModel`) owns a `CurrentPage :
PageViewModel` presented through a `TransitioningContentControl` (180 ms
cross-fade). Pages are created by `PageFactory` (id → view model; Home and
Settings resolve via DI, tool ids resolve to placeholders until their
milestone). Views map from view models through an **explicit** `ViewLocator`
table — no reflection, trim-safe (ADR-0003/0005). Cross-page navigation uses
`WeakReferenceMessenger` with `NavigateToToolMessage`.

## Design system

All visual decisions live in `src/Acroball.UI/Theme/`:

- **Palette.axaml** — semantic brushes only (`Q.Brush.Surface`,
  `Q.Brush.TextSecondary`, `Q.Brush.Accent`, …) in Light ("warm paper":
  `#FAFAF8` background, near-black warm text) and Dark ("deep ink":
  `#0E0F13`, desaturated light text) ThemeDictionaries, shared indigo accent
  (`#5B67E8` light / `#6E7BFF` dark). No view references a hex color.
- **Tokens.axaml** — corner radii (6/8/12).
- **Typography.axaml** — Inter, compact desktop ramp: h1 24/semibold,
  h2 17/semibold, body 13, caption 12. Body text inherits the window
  foreground; there is deliberately no global `TextBlock` foreground setter
  (it would defeat inheritance inside buttons).
- **Icons.axaml** — 26 Fluent System Icons as `StreamGeometry` resources
  keyed `Icon.*`; view models refer to icons by string key, resolved by
  `IconKeyToGeometryConverter`.
- **Controls.axaml** — button variants (`primary`, `ghost`, `danger`),
  the home-screen `tool-card` (custom template, 2 px hover lift with
  150 ms ease-out transform + shadow transitions), segmented radio control,
  sidebar list styling, tooltip. Restyled pseudo-states target
  `/template/ ContentPresenter#PART_ContentPresenter` because Fluent's own
  state styles set backgrounds there.

Motion defaults: 120–180 ms, ease-out, opacity/transform only.

Theme switching: `IThemeService` (Application) → `ThemeService` (UI) sets
`RequestedThemeVariant`; every brush is consumed via `DynamicResource`, so
the switch is live. "System" maps to `ThemeVariant.Default`.

## Threading and responsiveness

- All PDF operations run off the UI thread (engine implementations own
  this; view models just `await`).
- Rendering is sequential per the PDFium constraint; thumbnail strips
  populate progressively.
- Every long operation is cancellable and reports progress; the M2 tool
  pages surface both.
- Logging never blocks: writes go through a channel to one background
  consumer (ADR-0006).

## Settings, recent files, logs (ADR-0006/0007)

Per-OS data directory (override: `Acroball_DATA_DIR`), containing
`settings.json`, `recent.json` and `logs/Acroball-yyyyMMdd.log` (14-day
retention). JSON is source-generated (trim-safe), enums as strings, writes
are atomic (`*.tmp` + move). Corrupt files fall back to defaults with a
logged warning; persistence problems never block startup or close.

Window size/state persists on close (normal bounds only; maximized is
remembered as a flag).

## Testing

- **xUnit v3** across the solution; test projects are executables
  (`OutputType=Exe`) per the v3 model.
- Domain tests: pure logic (page-range parsing is the workhorse).
- Infrastructure tests: real files in per-test temp directories via
  `AppPaths(tempDir)` — no mocking of the filesystem.
- Engine integration tests generate fixture PDFs with PDFsharp itself and
  encode page identity in **geometry** (page *n* is 100+*n* points wide), so
  assertions never require text rendering or fonts — the suite stays green
  on bare CI runners with no system fonts installed.
- The remaining M2 work adds `Avalonia.Headless.XUnit` for
  shell/navigation/theming smoke tests alongside the tool pages.
- CI builds and tests on ubuntu/windows/macos (`.github/workflows/ci.yml`).

## Milestones

| # | Contents |
| --- | --- |
| M1 | Solution skeleton, design system, themed shell, settings/theme/logging, test harness. **This baseline.** |
| M2 | PDFsharp engine + integration tests (**done**); Merge and Split/Extract/Rotate tool pages, file pickers, progress/cancel UX, headless UI tests (remaining). |
| M3 | PDFium rendering service, thumbnail pipeline, visual Organize tool (drag-reorder, delete, rotate, cross-file compose). |
| M4 | Compress, Protect (encrypt/decrypt/permissions), Metadata editor; command palette; custom window chrome. |
| M5 | Plugin loading (ALC), plugin manager UI, SDK contribution points; accessibility pass. |
| M6 | Velopack packaging (Windows/macOS/Linux), self-update, code signing, 1.0. |

## Packaging plan (ADR-0005/0008)

Self-contained, IL-trimmed, ReadyToRun publishes per RID. NativeAOT is
excluded *by decision*: the plugin system requires runtime assembly loading.
Trim-safety is maintained continuously (source-gen JSON, compiled bindings,
explicit ViewLocator) rather than retrofitted.

## Accessibility checklist (tracked, M5 pass)

Keyboard reachability for every action (shortcuts exist from M1:
Ctrl+1…9 navigation), visible focus indication, AutomationProperties on
interactive elements, contrast ≥ 4.5:1 for text in both themes, respect
reduced-motion by keeping animation opacity/transform-only and short.

## Privacy

Everything is local. No network calls exist in M1–M5 at all; M6 adds exactly
one — the self-update check — which is user-visible and disableable.

