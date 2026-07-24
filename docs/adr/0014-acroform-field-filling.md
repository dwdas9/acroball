# ADR-0014: AcroForm field filling via PdfSharp.Pdf.AcroForms

**Status:** Accepted (M7)

## Context

The fourth and last of the viewer/editor features planned alongside the
Viewer (ADR-0011), Bookmarks (ADR-0012) and Annotations (ADR-0013): reading
and filling a PDF's AcroForm fields. This was scoped as the lowest-risk of
the four going in, since `PdfSharp.Pdf.AcroForms` — unlike
`PdfSharp.Pdf.Annotations` — is a mature, fully public, typed API in the
pinned `6.2.4` package: `PdfDocument.AcroForm`, `PdfAcroForm.Fields`, and
typed subclasses (`PdfTextField`, `PdfCheckBoxField`, `PdfComboBoxField`,
`PdfListBoxField`, `PdfRadioButtonField`, `PdfSignatureField`,
`PdfPushButtonField`) all exist and are reflectively confirmed present.

Two real, load-bearing gaps surfaced only by writing and running actual code
against this API (not by reading documentation), both worth recording since
they change how this feature had to be built:

1. **Every typed field constructor is non-public**, and
   `PdfAcroFieldCollection` has no `Add` method. PDFsharp's AcroForms API is
   built to *read and fill* forms authored by some other tool (Acrobat, a web
   form generator, …) — it cannot *author* a new form's field structure. This
   app never needed to author one before (every prior tool composes whole
   pages, not form widgets), so this had never come up.
2. **Merely reading a `PdfTextField` — not drawing one — makes PDFsharp
   eagerly construct an internal font, unconditionally**, and PDFsharp 6.x
   ships no default `IFontResolver`. Confirmed directly: enumerating
   `AcroForm.Fields` on a document with a text field throws
   `"No appropriate font found for family name 'Courier New'"` the instant
   the collection instantiates a `PdfTextField`, regardless of what the
   field's own `/DA` (default appearance) string specifies. No prior
   Acroball code path ever touched `PdfSharp.Drawing.XFont` (Compress
   recompresses images via SkiaSharp directly; the new Annotations content
   streams are hand-written text, not drawn through PDFsharp's font/graphics
   API), so this gap was invisible until this feature exercised it — on
   every platform, for every PDF containing a text field, including ones this
   app never wrote itself.

## Decision

- New `Acroball.Domain.Forms`: `FormFieldKind`, `PdfFormFieldInfo`,
  `FormFieldValue`. Checkbox/combo/list-box current values and options are
  all uniform export-value strings (e.g. `"/Yes"`/`"/Off"`) — no variant
  type; radio buttons use a 0-based option index string instead, since
  `PdfRadioButtonField` exposes `SelectedIndex`, not an export-value list.
- `IPdfEngine.GetFormFieldsAsync` (a direct read, like `GetOutlineAsync`) and
  `IPdfEngine.FillFormAsync`/`FillFormRequest` (mutating, file-in/file-out,
  wrapped in `FillFormJobRequest`/`FillFormJob` mirroring `ComposeJob`).
- **`SkiaSystemFontResolver`** (`Acroball.Infrastructure.Pdf`): implements
  `IFontResolver` by asking SkiaSharp's cross-platform `SKFontManager` to
  resolve a system font and extracting its real file bytes via
  `SKTypeface.OpenStream`. SkiaSharp is already a dependency (ADR-0009), so
  this adds no new package. Assigned once, globally, in
  `InfrastructureServiceCollectionExtensions.AddAcroballInfrastructure`
  (`GlobalFontSettings.FontResolver ??= ...`) — a `PdfSharp`-owned static,
  not a DI-managed service. Resolution *accuracy* doesn't matter for this
  app's purposes (Acroball never renders form-field text through PDFsharp's
  own font/graphics API — every glyph this app itself draws is hand-written
  content-stream bytes, ADR-0013), only that *some* real, loadable font
  satisfies PDFsharp's internal requirement so it doesn't throw.
- `PdfSharpEngine.GetFormFieldsAsync`/`FillFormAsync` read/write via
  `PdfAcroField`'s typed subclasses only — no authoring of new fields, no
  hand-built dictionaries (unlike Annotations, this phase didn't need that
  technique; the typed API already covers everything it needs to do).
  `PdfChoiceField` (base of ComboBox/ListBox) exposes `SelectedIndex` but no
  public options list, so `/Opt` is read directly via `Elements` — the same
  low-level-dictionary-read pattern already used elsewhere, just for reading
  this time, not authoring.
- `PdfDocument.AcroForm` throws `InvalidOperationException` rather than
  returning null when the catalog has no `/AcroForm` entry — verified
  directly, not assumed from its signature. `HasAcroForm(document)` checks
  `document.Internals.Catalog.Elements.ContainsKey("/AcroForm")` first
  instead of null-checking the property.
- `FillFormAsync` unconditionally sets `/NeedAppearances` on the AcroForm
  after writing values — the standard mitigation for viewers (SumatraPDF,
  Apple Preview) that don't regenerate appearance streams themselves on
  open. `FlattenAfterFill` sets every field's `ReadOnly = true`.
- UI: unlike Bookmarks/Annotations, this phase gets its **own tool**
  ("Fill Form") rather than folding into the Viewer — filling a form is a
  batch data-entry operation over a whole document, not something that
  needs the continuous-scroll viewer open. `FormViewModel`/`FormFieldViewModel`
  follow the "load info → edit → single job execute" shape (like
  Compress/Metadata) rather than Organize/Viewer's accumulate-then-execute
  shape. No keyboard shortcut: `Ctrl+0` was already claimed by the Viewer
  (ADR-0011) and `Ctrl+1`..`Ctrl+9` by the other nine pages; sidebar
  navigation only, per the existing "don't renumber existing bindings" rule.

## Consequences

- Test fixtures hand-author the AcroForm structure (merged field/widget
  dictionaries, an AcroForm catalog dictionary set via
  `document.Internals.Catalog.Elements["/AcroForm"]`) — the same low-level
  technique Annotations needed for authoring, even though production
  `FillFormAsync` itself never authors anything. This is intentional and
  matches the existing "generate PDFs on the fly, no embedded resource
  PDFs" test convention; it is simply the only way to get a *fillable*
  fixture PDF at all, given PDFsharp's typed field constructors are
  non-public.
- The checkbox fixture needed a real `/AP /N` dictionary defining named
  on/off appearance states — verified directly that a checkbox *without*
  one can still be *read* (PDFsharp reports sensible `CheckedName`/`UncheckedName`
  defaults) but throws `ArgumentNullException` from inside PDFsharp's own
  `PdfCheckBoxField.Checked` setter when *filled*. Real-world PDFs from
  Acrobat or any other form-authoring tool always include this dictionary,
  so this is a fixture-completeness requirement, not a production risk.
- `PdfSharpEngineTests` now also assigns `GlobalFontSettings.FontResolver`
  in its constructor (`??=`, matching the production composition-root
  assignment) — the test process never runs
  `AddAcroballInfrastructure`, so the equivalent one-time global assignment
  has to happen somewhere the tests actually execute.
- Radio button groups and list boxes are supported structurally through the
  same typed API path as CheckBox/ComboBox but were not exercised against a
  dedicated hand-built fixture in this milestone — hierarchical fields (a
  radio group's kid widgets) are a step beyond what the text/checkbox/combo
  fixture builds. Stated scope limit, not a silent gap.
