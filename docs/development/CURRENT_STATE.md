# Current State

Last updated: 2026-07-06

## Current Milestone
M4 — Compress, Protect, Metadata, sequenced one at a time per user decision
(Metadata first — shipped, commit `061f832`; then Protect; then Compress).
**Protect is now implemented end-to-end** (engine, Application jobs,
ViewModel, View, wiring, tests) and build/test-verified. Not yet committed —
do not assume it's in git until confirmed. Manual smoke test is in progress
with the user.

## Last Completed Action
Full Protect tool implementation, built and tested:
- `src/Acroball.Infrastructure/Pdf/PdfSharpEngine.cs` — `EncryptAsync`/
  `DecryptAsync` implemented via PDFsharp's `SecurityHandler`/
  `SecuritySettings`. **Real-world correction to the original plan**:
  PDFsharp itself throws `PdfSharp.PdfSharpException` ("At least a user or
  an owner password is required to encrypt the document.") if you attempt
  to save with a security handler attached but no password at all — the
  plan had assumed this scenario would silently succeed. Not an issue in
  practice because `EncryptionOptions.HasAnyPassword` is already enforced
  by `EncryptJobRequest.Validate()` before the engine is ever called.
- `tests/Acroball.Infrastructure.Tests/PdfSharpEngineTests.cs` — 5 new tests
  (password set/opens, permission flags round-trip, AES-128 vs AES-256,
  decrypt removes protection, encrypt-with-no-password throws per the point
  above). All pass.
- `src/Acroball.Application/Jobs/{Encrypt,Decrypt}Job{,Request}.cs` — new,
  mirrors `RotatePagesJob(Request)`/`UpdateMetadataJobRequest` shape exactly.
- `src/Acroball.UI/ViewModels/ProtectViewModel.cs` — new. Introduces
  UI-only `enum ProtectMode { Encrypt, Decrypt }`. On file selection, calls
  `InspectAsync` once purely to detect `InvalidPdfPasswordException` and
  auto-flip `Mode` to `Decrypt` (else stays `Encrypt`). Always-visible
  `CurrentPassword` field; Encrypt-only `UserPassword`/`OwnerPassword`/
  `Strength`/7 permission bools (all default true, no `ExtractForAccessibility`
  toggle — no PDFsharp 6.2.4 equivalent exists). Output suggestion:
  `{stem}-protected.pdf` / `{stem}-unprotected.pdf` depending on mode.
- `src/Acroball.UI/Views/ProtectView.axaml` + `.axaml.cs` — new, drag/drop
  single-file pattern copied verbatim from `RotateView.axaml.cs`. Uses the
  established `RadioButton Classes="segment"` + `EnumToBooleanConverter`
  pattern twice (Mode selector, Strength selector) and first-ever
  `PasswordChar="•"` masked `TextBox` usage in this codebase.
- Wiring: `UiServiceCollectionExtensions.cs`, `PageFactory.cs` (`"protect"`
  case), `ViewLocator.cs` — no `Views.` qualification needed, confirmed
  `ProtectView` doesn't collide with any `Avalonia.Controls` type (unlike
  `SplitView`).
- `tests/Acroball.UI.Tests/ProtectViewModelTests.cs` — 8 new tests, all
  passing (initial state, plain-file keeps Encrypt mode, encrypted-file
  auto-flips to Decrypt mode, Encrypt/Decrypt validation, both execute-success
  paths, reset).
- `dotnet test Acroball.sln` passes 112/112 (33 Domain + 4 Application +
  39 UI + 36 Infrastructure).

Manual smoke test (from the approved plan) is underway with the user:
step 1 confirmed via screenshot — initial page state renders correctly
(Add Password mode selected, all 7 permissions checked, AES-256 selected,
disabled button, correct validation message). Steps 2–8 (encrypt a real
file, confirm auto-flip on the output, wrong-password rejection, etc.) not
yet done.

## Current Blockers
None. Solution builds clean; full test suite passes. (Reminder: if
`Acroball.exe` is running, `dotnet build`/`dotnet run` on it will hit
MSB3026 file-lock errors — not a bug, close the running instance first.
One instance was left running for the current manual smoke test — do not
be surprised by this on session resume.)

## Next Immediate Task
Continue the manual smoke test with the user: have them drag a plain PDF
into the running Protect page, set a user password, choose an output
folder, save, then drag the *output* file back in to confirm Mode
auto-flips to "Remove Password". After the smoke test checklist is
complete (see the plan file's 8-step list), ask the user whether to commit
the Protect work (footprint of `dwdas`, no Claude co-author trailer, per
this session's established convention), then move to planning **Compress**
as the final M4 tool.

## Context Dependency Index

- C:\Users\dasd\.claude\plans\replicated-dazzling-pretzel.md
- src/Acroball.Infrastructure/Pdf/PdfSharpEngine.cs
- tests/Acroball.Infrastructure.Tests/PdfSharpEngineTests.cs
- src/Acroball.Application/Jobs/EncryptJobRequest.cs
- src/Acroball.Application/Jobs/EncryptJob.cs
- src/Acroball.Application/Jobs/DecryptJobRequest.cs
- src/Acroball.Application/Jobs/DecryptJob.cs
- src/Acroball.UI/ViewModels/ProtectViewModel.cs
- src/Acroball.UI/Views/ProtectView.axaml
- src/Acroball.UI/Views/ProtectView.axaml.cs
- tests/Acroball.UI.Tests/ProtectViewModelTests.cs
- src/Acroball.UI/ViewLocator.cs
