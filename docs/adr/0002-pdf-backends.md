# ADR-0002: PDFsharp for manipulation, PDFium for rendering, both behind abstractions

**Status:** Accepted (M1 baseline; implementations land M2/M3)

## Context

No single permissively-licensed .NET library both manipulates and rasterizes
PDFs well. iText and Ghostscript are capability-rich but AGPL — unacceptable
for an MIT-licensed app. QuestPDF is generation-oriented, not manipulation.

## Decision

- **Manipulation** (merge, split, extract, rotate, compose, encrypt, decrypt,
  metadata): **PDFsharp 6.2.x** (MIT). Supports AES-128 and AES-256
  encryption and import/export of pages across documents.
- **Rendering** (thumbnails, previews): **PDFium via PDFtoImage** (MIT
  wrapper; PDFium itself Apache/BSD).
- Both are hidden behind `IPdfEngine` and `IPdfRenderService` in
  Acroball.Application. UI code never sees a backend type.

## Consequences

- Two native/library stacks to ship, but each is best-in-class for its half.
- **PDFium is not thread-safe.** Render implementations must serialize all
  native calls behind an internal queue; thumbnail strips render
  sequentially. This constraint is part of the `IPdfRenderService` contract
  documentation.
- Compression (M4) is limited to what PDFsharp can do (stream recompression,
  object pruning, image re-encoding done by us); no Ghostscript-grade
  downsampling pipeline. Documented honestly in the tool UI.

