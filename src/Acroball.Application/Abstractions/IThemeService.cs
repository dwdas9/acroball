using Acroball.Application.Models;

namespace Acroball.Application.Abstractions;

/// <summary>
/// Applies a <see cref="ThemePreference"/> to the running UI. Implemented by
/// the presentation layer; kept as an abstraction so view models stay free of
/// UI framework types.
/// </summary>
public interface IThemeService
{
    /// <summary>Applies the preference immediately.</summary>
    void Apply(ThemePreference preference);
}

