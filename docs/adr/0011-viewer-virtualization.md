# ADR-0011: Viewer via ListBox virtualization, realize/derealize-driven rendering

**Status:** Accepted (M6)

## Context

Acroball had no way to actually look at a PDF's pages inside the app — every
existing tool (Merge, Split, Organize, Rotate, Compress, Encrypt/Decrypt,
Metadata) is a whole-document, file-in/file-out batch operation. `IPdfEngine`
and `IPdfRenderService` already existed and needed no new members: `GetPagesAsync`
returns per-page geometry and `IPdfRenderService.RenderPageAsync` (ADR-0002,
ADR-0010) rasterizes any page to PNG through PDFium. Organize already proved
this render path works for a grid of small thumbnails. The Viewer needed the
same render path used for full-size, continuously-scrollable pages, which
introduces a problem Organize never had to solve: Organize's page count is
whatever the user has actively added (small, no virtualization), while a
Viewer must handle an arbitrary, possibly large document without rendering
every page's full-size bitmap at once.

No virtualizing panel (`VirtualizingStackPanel`, `ItemsRepeater`, or similar)
existed anywhere in the codebase before this change; Organize's thumbnail
grid uses a plain `ItemsControl` + `WrapPanel` inside a `ScrollViewer`, which
does not virtualize.

## Decision

- `ViewerViewModel`/`ViewerPageViewModel` follow the same shape as
  `OrganizeViewModel`/`OrganizePageViewModel`: geometry (`GetPagesAsync`) is
  loaded eagerly and cheaply for every page up front; each page's bitmap is
  loaded lazily, later, per page.
- The page list is a `ListBox` with `VirtualizingStackPanel` as its
  `ItemsPanel` (`Orientation="Vertical"`), not a bare `ItemsControl`. Avalonia's
  `ListBox` wires the scroll-info protocol a virtualizing panel needs to
  actually virtualize; a plain `ItemsControl` does not.
- Lazy per-page bitmap loading is driven by the `ListBox`'s
  `ContainerPrepared`/`ContainerClearing` events in `ViewerView.axaml.cs`,
  which call `ViewerViewModel.LoadPageImageAsync`/`UnloadPageImage`. This
  extends Organize's per-tile `CancellationTokenSource` pattern (cancel a
  page's in-flight render when it's removed) from an add/remove trigger to a
  realize/derealize trigger: a page scrolled out of view cancels its render
  and frees its bitmap, a page scrolled into view (re)starts one.
- `ViewerPageViewModel.PlaceholderHeightPx` reserves each page's on-screen
  slot from geometry alone (`WidthPoints`/`HeightPoints`, rotation-swapped)
  before any bitmap arrives, so the virtualized list's layout never jumps
  once a page's render completes — the reserved aspect ratio and the
  eventually-rendered bitmap's aspect ratio are the same value.
- Render width is fixed at `ViewerPageViewModel.DisplayWidthPx` (900px); there
  is no re-render on window resize or a zoom control in this milestone. This
  is a deliberate, stated scope limit, not an oversight.
- `ViewerViewModel` is registered `AddTransient`, identical to every other
  page — navigating away discards the open document, consistent with
  Organize and every other tool. No new DI lifetime was introduced.
- The Viewer is reachable via the sidebar and `Ctrl+0` (the only unused
  single-digit gesture; `Ctrl+1`..`Ctrl+9` were already claimed by the 9
  existing pages and were not renumbered).

## Consequences

- Fast scrolling can queue several concurrent render requests behind
  `PdfRenderService`'s single-slot PDFium semaphore (ADR-0010); the
  cancel-on-derealize behavior means most of those are cancelled before they
  complete rather than wasting PDFium time, but a very fast scroll still
  briefly shows "Loading…" placeholders rather than instantaneous bitmaps.
  This mirrors Organize's existing behavior under rapid add/remove and was
  accepted there for the same reason: correctness (no crash, no stale image)
  matters more than eliminating every transient placeholder frame.
- Because Bitmap decoding requires Avalonia's `IPlatformRenderInterface`,
  `ViewerViewModelTests` cannot exercise a real successful decode in a plain
  xUnit host (no running `Application`) — the same limitation that already
  meant `OrganizeViewModelTests` never exercises `LoadThumbnailAsync`'s
  decode step. Tests instead verify the render-service call contract and the
  loading/error state machine directly; genuine bitmap rendering is only
  provable by running the real desktop app.
- This milestone's automated verification (Debug + Release build, full test
  suite) is green, but an interactive, watched confirmation of Viewer inside
  the real running app (open a multi-page PDF, scroll through it, confirm
  pages render) was deferred to manual verification by the user rather than
  automated: driving the app via desktop-wide screen-coordinate automation
  during this session risked (and once did) capturing unrelated windows on
  the shared desktop, which was judged not an acceptable trade-off for a
  scripted smoke test.
