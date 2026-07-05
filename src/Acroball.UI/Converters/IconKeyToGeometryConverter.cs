using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Avalonia.Styling;

namespace Acroball.UI.Converters;

/// <summary>
/// Resolves an icon key such as <c>"Merge"</c> to the <see cref="StreamGeometry"/>
/// resource registered as <c>"Icon.Merge"</c> in Theme/Icons.axaml. Lets view
/// models refer to icons by plain strings, keeping them free of UI types.
/// </summary>
public sealed class IconKeyToGeometryConverter : IValueConverter
{
    /// <inheritdoc />
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string key || key.Length == 0)
        {
            return null;
        }

        var app = Avalonia.Application.Current;
        // Use the Avalonia resource lookup that works with the current package version.
        if (app is not null && app.TryGetResource($"Icon.{key}", null, out var resource))
        {
            return resource as StreamGeometry;
        }

        return null;
    }

    /// <inheritdoc />
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

