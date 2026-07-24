using Acroball.Domain.Forms;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Acroball.UI.ViewModels;

/// <summary>
/// One editable row in the Fill Form tool, wrapping a <see cref="PdfFormFieldInfo"/>
/// with the current, possibly user-edited, value.
/// </summary>
public sealed partial class FormFieldViewModel : ObservableObject
{
    /// <summary>Creates the row from the field as read from the document.</summary>
    public FormFieldViewModel(PdfFormFieldInfo info)
    {
        FullyQualifiedName = info.FullyQualifiedName;
        Kind = info.Kind;
        IsReadOnly = info.IsReadOnly;
        Options = info.Options;
        _value = info.CurrentValue ?? string.Empty;
    }

    /// <summary>The field's full name, used to address it when filling.</summary>
    public string FullyQualifiedName { get; }

    /// <summary>The field's type.</summary>
    public FormFieldKind Kind { get; }

    /// <summary>Whether the field is marked read-only in the PDF.</summary>
    public bool IsReadOnly { get; }

    /// <summary>Selectable export values, for CheckBox/ComboBox/ListBox; null otherwise.</summary>
    public IReadOnlyList<string>? Options { get; }

    /// <summary>The current (possibly edited) value, as an export-value string.</summary>
    [ObservableProperty]
    private string _value;

    /// <summary>Whether this is a free-text field.</summary>
    public bool IsText => Kind == FormFieldKind.Text;

    /// <summary>Whether this is a checkbox.</summary>
    public bool IsCheckBox => Kind == FormFieldKind.CheckBox;

    /// <summary>Whether this is a dropdown or list selection.</summary>
    public bool IsChoice => Kind is FormFieldKind.ComboBox or FormFieldKind.ListBox;

    /// <summary>Whether this is one option of a radio group (edited as a 0-based option index).</summary>
    public bool IsRadioButton => Kind == FormFieldKind.RadioButton;

    /// <summary>Whether this field can be written by a fill (excludes push buttons, signatures and unrecognized kinds).</summary>
    public bool IsFillable => Kind is FormFieldKind.Text or FormFieldKind.CheckBox or FormFieldKind.ComboBox or FormFieldKind.ListBox or FormFieldKind.RadioButton;

    /// <summary>Checkbox-friendly view over <see cref="Value"/>, using <see cref="Options"/>[0] as the checked export value (set by <c>PdfSharpEngine.ToFieldInfo</c>).</summary>
    public bool IsChecked
    {
        get => Options is { Count: > 0 } && Value == Options[0];
        set
        {
            if (Options is { Count: > 1 })
            {
                Value = value ? Options[0] : Options[1];
            }
        }
    }

    partial void OnValueChanged(string value) => OnPropertyChanged(nameof(IsChecked));
}
