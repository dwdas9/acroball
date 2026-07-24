using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Acroball.Domain.Annotations;

namespace Acroball.UI.Converters;

/// <summary>Converts an <see cref="AnnotationColor"/> to a solid Avalonia brush, for the Viewer's annotation toolbar swatches.</summary>
public sealed class AnnotationColorToBrushConverter : IValueConverter
{
    /// <inheritdoc />
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is AnnotationColor color ? new SolidColorBrush(Color.FromRgb(color.R, color.G, color.B)) : null;

    /// <inheritdoc />
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
