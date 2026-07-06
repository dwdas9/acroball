# Current State

Last updated: 2026-07-06

## Current Milestone
M4 — Compress, Protect, Metadata, sequenced one at a time per user decision
(Metadata first, then Protect, then Compress; each planned/reviewed
separately since the three are not equal-sized work). **Metadata is now
complete** (user confirmed manual smoke test — pre-fill from existing PDF
metadata works correctly in the running app). Not yet committed — do not
assume it's in git until confirmed. Next up: plan **Protect**. Full plan on
disk at `C:\Users\dasd\.claude\plans\replicated-dazzling-pretzel.md` (will
need to be overwritten again when Protect planning starts, same as it was
overwritten from Split/Extract/Rotate's plan for Metadata).

## Last Completed Action
Metadata tool production code + tests written and build/test verified.
`IPdfEngine.UpdateMetadataAsync` was already fully implemented and tested
before this work (unlike Compress/Protect, which are still
`NotSupportedException` stubs) — this was purely additive UI/Application-layer
work mirroring `ExtractViewModel`/`ExtractView` closely, plus a new
pre-fill-from-existing-metadata step. `dotnet test Acroball.sln` passes
99/99 (33 Domain + 4 Application + 31 UI + 31 Infrastructure).

Files created/modified this session for Metadata:
- `src/Acroball.Application/Jobs/UpdateMetadataJobRequest.cs`, `UpdateMetadataJob.cs` (new, build-verified)
- `src/Acroball.UI/ViewModels/MetadataViewModel.cs` (new, build-verified) — editable fields `DocumentTitle`/`Author`/`Subject`/`Keywords`/`Creator`/`CreationDate`; deliberately does **not** expose `Producer`/`ModificationDate` (engine ignores writes to them)
- `src/Acroball.UI/Views/MetadataView.axaml` + `.axaml.cs` (new, build-verified) — uses a `DatePicker` for `CreationDate` (first use of that control in the codebase, but it's stock Avalonia)
- Wiring edits: `UiServiceCollectionExtensions.cs`, `PageFactory.cs`, `ViewLocator.cs` (3 lines each, mirroring the `rotate`/`extract` entries — no `Views.` qualification needed since `MetadataView` doesn't collide with any `Avalonia.Controls` type)
- `tests/Acroball.UI.Tests/MetadataViewModelTests.cs` (new, 6 tests, all passing) — includes a regression guard confirming a blanked field is sent as `""` not `null` in the `UpdateMetadataJobRequest`

**Known, accepted gap** (documented in a code comment on `MetadataViewModel.CreationDate`
and in the plan file): clearing the `DatePicker` on a document that already
has a creation date will NOT clear it in the output — the engine treats a
`null` `CreationDate` as "leave unchanged," and there's no sentinel for
"clear it" the way `string.Empty` serves that role for the string fields.
Deliberately not fixed (would require new engine-layer scope, out of bounds
for this milestone).

## Current Blockers
None. (Reminder from last milestone: if the Acroball desktop app is
currently running, a `dotnet build`/`dotnet run` will hit MSB3026 file-lock
errors on its own output DLLs — not a bug, just close the running instance
first.)

## Next Immediate Task
Ask the user whether to commit the Metadata work now, and whether to start
planning **Protect** (Encrypt/Decrypt) next. Protect needs real new backend
work (PDFsharp has built-in AES-128/256 support per ADR-0002, so it's "call
an existing library API," not crypto-from-scratch, but
`EncryptionOptions`/`PdfPermissions` bit mapping to PDFsharp's permission
model still needs actual implementation work in `PdfSharpEngine.cs`, unlike
Metadata/Split/Extract/Rotate which were UI-only). Do not start Protect
implementation without a fresh plan/review cycle (overwrite the plan file),
per the user's explicit one-at-a-time sequencing decision. After Protect:
Compress (largest, most open-ended — hand-rolled image re-encoding, no
library support).

## Context Dependency Index

- C:\Users\dasd\.claude\plans\replicated-dazzling-pretzel.md
- src/Acroball.UI/ViewModels/MetadataViewModel.cs
- src/Acroball.UI/Views/MetadataView.axaml
- src/Acroball.Domain/EncryptionOptions.cs
- src/Acroball.Domain/PdfPermissions.cs
- src/Acroball.Infrastructure/Pdf/PdfSharpEngine.cs
- docs/adr/0002-pdf-backends.md
