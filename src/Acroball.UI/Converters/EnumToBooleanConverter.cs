using System.Globalization;
using Avalonia.Data;
using Avalonia.Data.Converters;

namespace Acroball.UI.Converters;

/// <summary>
/// Two-way converter binding an enum property to a group of radio buttons:
/// <c>IsChecked="{Binding Theme, Converter=..., ConverterParameter=Dark}"</c>.
/// </summary>
public sealed class EnumToBooleanConverter : IValueConverter
{
    /// <inheritdoc />
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is not null
           && parameter is not null
           && string.Equals(value.ToString(), parameter.ToString(), StringComparison.Ordinal);

    /// <inheritdoc />
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is true && parameter is not null)
        {
            return Enum.Parse(targetType, parameter.ToString()!, ignoreCase: false);
        }

        // Unchecking a radio button carries no information; leave the source alone.
        return BindingOperations.DoNothing;
    }
}

