namespace Acroball.Domain.Forms;

/// <summary>The kind of AcroForm field a <see cref="PdfFormFieldInfo"/> describes.</summary>
public enum FormFieldKind
{
    /// <summary>A free-text input.</summary>
    Text,

    /// <summary>A single checkbox.</summary>
    CheckBox,

    /// <summary>One option of a radio button group.</summary>
    RadioButton,

    /// <summary>A dropdown selection.</summary>
    ComboBox,

    /// <summary>A list selection.</summary>
    ListBox,

    /// <summary>A push button — not a data field, never fillable.</summary>
    PushButton,

    /// <summary>A digital signature field — not fillable by this app.</summary>
    Signature,

    /// <summary>A field kind this app doesn't recognize; read-only, never fillable.</summary>
    Unsupported,
}

/// <summary>
/// One field read from a PDF's AcroForm. Checkbox/radio/combo/list current
/// values and options are all uniform export-value strings — there is no
/// variant type; the caller maps bool/index concerns to and from strings.
/// </summary>
/// <param name="FullyQualifiedName">The field's full name, used to address it when filling.</param>
/// <param name="Kind">The field's type.</param>
/// <param name="IsReadOnly">Whether the field is marked read-only in the PDF.</param>
/// <param name="CurrentValue">The field's current value, if any.</param>
/// <param name="Options">Selectable export values, for CheckBox/ComboBox/ListBox; null otherwise.</param>
public sealed record PdfFormFieldInfo(
    string FullyQualifiedName,
    FormFieldKind Kind,
    bool IsReadOnly,
    string? CurrentValue,
    IReadOnlyList<string>? Options);

/// <summary>One field value to write during a fill.</summary>
/// <param name="FullyQualifiedName">The target field's full name, matched against <see cref="PdfFormFieldInfo.FullyQualifiedName"/>.</param>
/// <param name="Value">
/// The value to set: free text for Text fields, an export value from
/// <see cref="PdfFormFieldInfo.Options"/> for CheckBox/ComboBox/ListBox, or a
/// 0-based option index for RadioButton.
/// </param>
public sealed record FormFieldValue(string FullyQualifiedName, string Value);
