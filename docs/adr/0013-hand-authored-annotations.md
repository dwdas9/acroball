# ADR-0013: Hand-authored annotations (Highlight/FreeText/Ink/Square)

**Status:** Accepted (M6)

## Context

With the Viewer (ADR-0011) and outline navigation (ADR-0012) in place, the
next step was letting the user mark a document up — highlight, sticky
free-text notes, freehand ink, rectangles — and save that markup into the
PDF. This was flagged as the highest-risk of the four viewer/editor features
before implementation began, and a spike was run before writing any UI to
resolve the risk decisively rather than discover it mid-build.

The spike fetched the actual `PdfSharp` `6.2.4` package (the version pinned
in `Directory.Packages.props`, not `master`, which has unreleased future
work) and reflected over it directly:

- `PdfSharp.Pdf.Annotations` contains exactly: `PdfAnnotation` (abstract),
  `PdfAnnotations` (collection), `PdfLinkAnnotation`, `PdfRubberStampAnnotation`,
  `PdfTextAnnotation` (a sticky-note popup, not free text). **No
  `PdfHighlightAnnotation`, `PdfFreeTextAnnotation`, `PdfInkAnnotation`, or
  `PdfSquareAnnotation` exist in 6.2.4.**
- `PdfAnnotation` itself is `abstract` with only a `protected` parameterless
  and `protected PdfAnnotation(PdfDocument)` constructor — it cannot be
  instantiated directly for a custom `/Subtype` either.
- What **is** public and sufficient: `PdfDictionary(PdfDocument)` (public
  ctor), `PdfDictionary.CreateStream(byte[])` (public), `PdfArray(PdfDocument)`
  / `PdfArray(PdfDocument, PdfItem[])` (public), `PdfReal`/`PdfInteger` (public
  numeric `PdfItem`s), `PdfPage.Annotations.Elements.Add(PdfItem)` (inherited
  from `PdfArray`, accepts a raw dictionary), and
  `PdfDocument.Internals.AddObject(PdfObject)` (public — registers a new
  indirect object). This is the same class of API `PdfSharpEngine.CompressAsync`/
  `TryRecompressImage` already uses to *mutate* existing objects (ADR-0009);
  this phase extends it one step further, to *originate* brand-new ones.
- A full end-to-end spike (build a hand-authored Square annotation → save →
  reopen → render via `PdfRenderService`) confirmed the structure round-trips
  correctly, **and** separately surfaced that `PDFtoImage.RenderOptions.WithAnnotations`
  defaults to `false` — meaning the shipped `PdfRenderService.RenderCore`
  (used by both Organize's thumbnails and the Viewer) never rendered
  annotations at all, for any file, before this change. This was invisible
  until now because no annotation existed anywhere to fail to render.

## Decision

- New `Acroball.Domain.Annotations` namespace: `AnnotationKind`,
  `AnnotationColor`, `QuadPoints`, `InkPoint`, `InkStroke`, and the
  `AnnotationEdit` hierarchy (`HighlightAnnotationEdit`, `FreeTextAnnotationEdit`,
  `InkAnnotationEdit`, `SquareAnnotationEdit`). All coordinates are PDF
  points, page-local, bottom-left origin, **unrotated** page space — the
  Viewer maps screen pixels to this space itself (see below), never the
  engine.
- New `IPdfEngine.SaveAnnotationsAsync` / `SaveAnnotationsRequest`, following
  the exact file-in/file-out shape every other mutating engine method uses.
  Mutating → `SaveAnnotationsJobRequest`/`SaveAnnotationsJob`, structural
  copies of `ComposeJobRequest`/`ComposeJob`.
- `PdfSharpEngine.SaveAnnotationsAsync` hand-authors every annotation: a
  per-kind content-stream builder emits raw PDF operators (Highlight: an
  `ExtGState` for `/ca` opacity + per-quad fill; FreeText: `BT`/`Tf`/`Td`/`Tj`/`ET`
  against an unembedded base-14 `/Helvetica` font resource; Ink: `m`/`l`
  polylines per stroke with rounded caps/joins; Square: `re`/`S`, optionally
  `re`/`f` first for a fill). Each is wrapped as a hand-built `/XObject
  /Subtype /Form` dictionary (`CreateStream` + `Internals.AddObject`), then an
  annotation dictionary (`/Type /Annot`, `/Subtype`, `/Rect`, `/C`, `/CA`,
  `/AP << /N ref >>`, plus `/QuadPoints` for Highlight and `/InkList` for Ink
  for interoperability with viewers that read those directly) — also
  registered via `Internals.AddObject` before being appended to
  `page.Annotations.Elements` (an unregistered dictionary added there would
  serialize as an unreferenced direct value, not a real annotation — the
  spike caught this by asserting the reopened `/Annots` entry is a
  `PdfReference`, not a `PdfDictionary`).
- Content-stream numbers are formatted with `CultureInfo.InvariantCulture`
  (`0.###`) — content streams are not locale-aware, and formatting with the
  current culture would silently corrupt the stream's decimal points on
  many systems. This is a real bug class this phase introduces the pattern
  to avoid, since no prior engine code emitted raw content-stream text.
- **`PdfRenderService.RenderCore` now sets `WithAnnotations: true`** on every
  render call. Without this, annotations saved by this feature would never
  be visible in Acroball's own Viewer or Organize thumbnails, even though
  they'd be spec-correct in the file. This is a one-line fix to existing
  Phase-1 code, made necessary by — and only discovered because of — this
  phase's spike.
- UI: no new tool/page, same reasoning as bookmarks (ADR-0012) — annotating
  has no meaning without an already-open, already-rendered document.
  `ViewerViewModel` gains tool/color selection state, `Add*Annotation`
  methods (called by `ViewerView` code-behind after a pointer interaction),
  and a save flow using `IJobExecutor` (a new constructor dependency on
  `ViewerViewModel`, already registered in DI via
  `InfrastructureServiceCollectionExtensions`). Each `ViewerPageViewModel`
  gained `Annotations` (the PDF-space `AnnotationEdit`s pending save) and
  `PreviewShapes` (a parallel, UI-only screen-space visual — captured
  directly from the same pointer interaction, not derived from
  `Annotations`, so no inverse-of-a-mapping code was needed). Rasterization
  of the real annotation only happens once, on save, matching the plan's
  "not by re-rendering the PDF" intent.
- Screen→PDF mapping lives in `Acroball.UI.Services.AnnotationCoordinateMapper`,
  a small static, independently unit-tested class. It must invert the
  page's rotation, not just scale, because PDFium bakes `/Rotate` into the
  rendered bitmap (ADR-0011) — a point drawn on a 90°-rotated page's
  displayed bitmap is not a simple scale of the unrotated `/Rect` space the
  engine expects. All four `Rotation` cases were derived independently twice
  (forward-rotating each page corner, then checking the inverse formula
  lands back on it) and are covered by `AnnotationCoordinateMapperTests`.
- **Stated scope limits, not oversights**: FreeText renders a single line of
  ASCII-only text (no word wrap, no non-Latin glyphs — the base-14 Helvetica
  font isn't embedded and has no custom `/Encoding`); there is no live
  rubber-band preview while dragging a Square/Highlight/Ink shape — the
  preview appears only once the pointer is released, not during the drag.

## Consequences

- This phase was budgeted as the hardest of the four (Viewer, Bookmarks,
  Annotations, Forms) before implementation, based on the confirmed API gap
  above. In practice, once the low-level object-authoring pattern was
  proven by the Square spike, all four annotation kinds' content-stream
  builders followed the same shape and passed their round-trip and
  pixel-rendering tests on the first real attempt — the risk was concentrated
  in *discovering* the right approach, not in repeating it four times.
- `PdfSharpEngineTests` now constructs `PdfRenderService` directly (to prove
  annotations are visible pixels, not just structurally-present dictionary
  entries) and is therefore platform-attributed
  (`[SupportedOSPlatform("windows"/"macos"/"linux")]`), the same as
  `PdfRenderServiceTests`, per the propagation rule in ADR-0010.
- Any third-party PDF viewer's tolerance for a hand-built, non-PDFsharp-typed
  annotation is asserted here only via this app's own PDFium-backed render
  path; genuine cross-viewer compatibility (Acrobat, browser viewers, etc.)
  was not verified as part of automated tests, since automated proof of that
  is beyond xUnit's reach — the plan flagged this and a manual open-in-a-real-viewer
  check remains outstanding (see `CURRENT_STATE.md`).
