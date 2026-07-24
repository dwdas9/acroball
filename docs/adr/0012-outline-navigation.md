# ADR-0012: Bookmark/outline navigation via PdfSharp.Pdf.PdfOutline

**Status:** Accepted (M6)

## Context

With the Viewer (ADR-0011) in place, the next step toward Acrobat-style
viewing was navigating a document by its bookmark (outline) tree rather than
only by scrolling. `IPdfEngine` had no outline-reading member and no Domain
type for it — this was greenfield, unlike Compress/Encrypt which extended
existing PDFsharp usage.

`PdfSharp.Pdf.PdfOutline`/`PdfOutlineCollection` turned out to be a fully
public, mature API (confirmed against the actual pinned `6.2.4` package, not
assumed): `PdfDocument.Outlines` is a `PdfOutlineCollection` implementing
`IList<PdfOutline>`; each `PdfOutline` exposes `Title`, `DestinationPage`
(a `PdfPage` reference, not an index), `Opened`, `HasChildren`, and a nested
`Outlines` collection for children. There is, however, no `IndexOf(PdfPage)`
or similar lookup on `PdfPages` — resolving a `PdfOutline.DestinationPage`
back to a 1-based page number requires a linear scan comparing object
identity against `document.Pages[i]`.

## Decision

- New Domain type `PdfOutlineNode(Title, DestinationPageNumber, IsExpanded,
  Children)`. `DestinationPageNumber` is nullable: `null` when a bookmark
  targets something other than a simple in-document page (a named
  destination or external URI), which this reads as unresolvable rather than
  guessing.
- New `IPdfEngine.GetOutlineAsync(filePath, password, cancellationToken)`.
  This is a pure read, called directly from `ViewerViewModel` alongside
  `GetPagesAsync` — not wrapped in a `Job`, matching how `InspectAsync` is
  already called directly rather than through `IJobExecutor` (jobs are for
  mutating, progress-tracked, file-writing operations; this is neither).
- `PdfSharpEngine.GetOutlineAsync` recursively walks `document.Outlines`,
  resolving each `DestinationPage` to a page number via a linear identity
  scan (`ReferenceEquals(document.Pages[i], outline.DestinationPage)`) since
  no direct index lookup exists. Recursion depth is capped at 64, mirroring
  the `depth > 4` guard already in `PdfSharpEngine.CollectImageXObjects`,
  against malformed or cyclic outline dictionaries in hostile files.
- Scope is deliberately navigation-only: no add/rename/reorder/delete of
  bookmarks. Editing the outline would need a mutating `UpdateOutlineAsync`
  plus a `Job` pair later; this is a stated non-goal for this milestone, not
  a silent omission.
- No new tool/page. The bookmark tree is a panel inside `ViewerView`
  (`ViewerViewModel.Outline`, populated alongside `Pages` when a document
  opens), not a separate `ToolCatalog` entry — a bookmark tree has no
  meaning without an already-open, already-rendered document, and every page
  view model in this app is `AddTransient` with no cross-navigation state
  sharing, so a standalone "Bookmarks" tool could never share a document
  instance with a separately-instantiated Viewer tool anyway.
- Clicking a bookmark calls `ViewerViewModel.RequestScrollToPage(int)`, which
  raises a `ScrollToPageRequested` event; `ViewerView` code-behind is the
  only subscriber and turns that into `PagesList.ScrollIntoView(...)`. This
  keeps the "should we scroll, and to where" decision on the (testable)
  ViewModel and only the actual scroll mechanics on the (untestable-without-
  a-real-window) View, the same split already used for render
  cancellation in ADR-0011.
- An outline read failure is non-fatal to opening the document: it's caught,
  logged, and leaves `Outline` empty rather than blocking page viewing, since
  bookmarks are a navigation aid, not core to the Viewer's purpose.

## Consequences

- `PdfOutline.Opened` does not round-trip through a PDFsharp save/reopen in
  6.2.4 — verified directly: an outline entry written with `opened: true`
  reads back `false` after `document.Save()` followed by `PdfReader.Open()`,
  regardless of what was originally set. `PdfOutlineNode.IsExpanded` is
  therefore best-effort and will currently always read as collapsed for any
  file this app (or PDFsharp generally) wrote; files with true PDF-spec
  `/Count` semantics from a different producer may still resolve correctly,
  since this reads whatever PDFsharp itself parsed on open, independent of
  the round-trip issue found for PDFsharp-written files. This is an upstream
  PDFsharp limitation, not a bug in the mapping here — see
  `PdfSharpEngineTests.GetOutline_opened_flag_does_not_round_trip_through_pdfsharp_save`.
- Because outline reading is a linear scan over the page list per bookmark,
  a document with very deep or very wide bookmark trees pays an O(bookmarks
  × pages) cost on open. Real-world outlines and page counts make this
  negligible; it was not optimized further given no evidence it needs to be.
