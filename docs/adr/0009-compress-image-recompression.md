# ADR-0009: SkiaSharp for Compress image recompression, scoped to JPEG/RGB/Gray

**Status:** Accepted (M4)

## Context

ADR-0002 scoped Compress to what PDFsharp does natively: content-stream
recompression and dropping unreferenced objects. That has no real effect on
the dominant case for oversized PDFs — high-resolution embedded photos —
because PDFsharp cannot decode or re-encode image data. Closing that gap
needs an image codec, which is a new dependency and therefore a licensing
question the project has cared about since ADR-0002 (MIT-only, no AGPL).

Avalonia 12 already pulls in SkiaSharp 3.x transitively as its rendering
backend. SkiaSharp is MIT-licensed, cross-platform, and can decode, resize and
re-encode JPEG. Choosing it adds no new dependency tree and no licensing
question, unlike Six Labors ImageSharp (Split License) which was the other
candidate.

## Decision

- Add an explicit `PackageReference` to `SkiaSharp` in Acroball.Infrastructure,
  pinned to the version Avalonia already resolves.
- `CompressAsync` always rebuilds the document page-by-page into a new
  `PdfDocument` (the same import mechanism Merge/Split/Extract use), which
  drops objects no longer reachable from any page — this is the "object
  pruning" half of Compress and applies to every profile, including
  `Lossless`.
- For `Balanced` and `Aggressive`, additionally walk each page's (and each
  directly-nested Form XObject's) `/XObject` resources and recompress images
  that meet all of:
  - `/Filter` is exactly `/DCTDecode` (already-JPEG; no chained filters)
  - `/ColorSpace` is exactly `/DeviceRGB` or `/DeviceGray` (no CMYK, Indexed,
    ICCBased, or other array-form color spaces)
  - no `/SMask`, `/Mask`, or `/ImageMask` entry (no transparency/stencil
    masks, which would need to be resized in lockstep with the base image)

  Matching images are decoded via `SKBitmap.Decode`, downsampled to a
  profile-specific max edge length, re-encoded as JPEG at a profile-specific
  quality, and the replacement is only kept if it is strictly smaller than
  the original — otherwise the original bytes are left in place. Any image
  that fails to decode, or falls outside the criteria above, passes through
  untouched.
- `Lossless` never touches image data, only the rebuild/prune step.

## Consequences

- Compress has a real, honestly-scoped effect on the common case (RGB/Gray
  JPEGs, which cover the vast majority of scanned and photo-heavy PDFs)
  without inheriting a new dependency license to review.
- CMYK photos, indexed/palette images, PNG-in-PDF (Flate-encoded raster
  images), and images with alpha/soft masks are left uncompressed by design.
  This is a known, documented gap, not a silent limitation — call it out in
  the tool's UI copy alongside the profile picker.
- Object identity is used to de-duplicate images shared across pages (a
  logo repeated on every page is recompressed once), matching PDFsharp's own
  import de-duplication behavior.
