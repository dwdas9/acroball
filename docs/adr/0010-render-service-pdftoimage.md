# ADR-0010: PdfRenderService implemented over PDFtoImage/PDFium

**Status:** Accepted (M3)

## Context

ADR-0002 already named PDFium (via the PDFtoImage MIT wrapper) as the
rendering backend for `IPdfRenderService`, reserved for the Milestone 3
visual page organizer. That interface existed with no implementation and no
package reference until this session, when Organize (page reorder/rotate/
delete/move) was picked up as the second M3/M4 gap left behind when the
project jumped ahead to M5 packaging.

## Decision

- Add an explicit `PackageReference` to `PDFtoImage` in Acroball.Infrastructure.
- `PdfRenderService : IPdfRenderService` calls `PDFtoImage.Conversion.ToImage`
  with `RenderOptions(Width: targetWidthPx, WithAspectRatio: true)`, re-encodes
  the returned `SKBitmap` to PNG via SkiaSharp (already a dependency, ADR-0009),
  and returns it as a `RenderedPageImage`.
- PDFium is not thread-safe (ADR-0002). Every render call is serialized
  behind a single-slot `SemaphoreSlim` inside `PdfRenderService`; concurrent
  callers queue rather than racing the native library. This makes the service
  safe to register as a singleton.
- PDFtoImage's `Conversion` APIs carry `[SupportedOSPlatform]` attributes for
  six platforms (Android, iOS, Linux, macCatalyst, macOS, Windows); this app
  only ships for the three desktop ones (ADR-0003). Rather than fight the
  platform-compatibility analyzer project-wide, the same three
  `[SupportedOSPlatform("windows"/"macos"/"linux")]` attributes are placed on
  `PdfRenderService`, on `AddAcroballInfrastructure`, and on the Desktop
  composition root's `DesktopComposition.BuildServiceProvider`/
  `Program.Main`/`Program.BuildAvaloniaApp` — the only call chain that ever
  touches this type. (The `<SupportedPlatform>` MSBuild item was tried first
  and does not suppress this warning for a plain `net10.0` TFM; it did not
  reach the built assembly's platform metadata the way the per-member
  attribute does.)
- Exceptions map onto the existing `Acroball.Domain.Exceptions` hierarchy the
  same way `PdfSharpEngine` already does: `PdfPasswordProtectedException` →
  `InvalidPdfPasswordException`, any other `PdfException` → `CorruptPdfException`.

## Consequences

- Organize's thumbnail grid renders real pages, not placeholders — decode,
  encode and native-library serialization are all exercised by integration
  tests in `PdfRenderServiceTests`, including a concurrent-call test that
  confirms the semaphore doesn't deadlock or corrupt output under load.
- Every future call site that constructs or invokes `PdfRenderService`
  (directly, not through the `IPdfRenderService` interface) needs the same
  three `[SupportedOSPlatform]` attributes, or the platform-compatibility
  analyzer will fail the build (`TreatWarningsAsErrors` is on solution-wide).
  This is a one-time cost per call site, not per call.
