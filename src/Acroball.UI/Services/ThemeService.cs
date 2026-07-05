using Avalonia.Styling;
using Acroball.Application.Abstractions;
using Acroball.Application.Models;

namespace Acroball.UI.Services;

/// <summary>
/// Applies a theme preference by switching the application's requested theme
/// variant; the palette's ThemeDictionaries do the rest.
/// </summary>
public sealed class ThemeService : IThemeService
{
    /// <inheritdoc />
    public void Apply(ThemePreference preference)
    {
        var app = Avalonia.Application.Current;
        if (app is null)
        {
            return;
        }

        app.RequestedThemeVariant = preference switch
        {
            ThemePreference.Light => ThemeVariant.Light,
            ThemePreference.Dark => ThemeVariant.Dark,
            _ => ThemeVariant.Default,
        };
    }
}

