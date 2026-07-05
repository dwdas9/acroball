namespace Acroball.Application.Models;

/// <summary>
/// User-facing application settings, persisted as JSON. Immutable: use
/// <c>with</c> expressions through <see cref="Abstractions.IAppSettingsService.Update"/>.
/// </summary>
public sealed record AppSettings
{
    /// <summary>Theme choice. Defaults to following the OS.</summary>
    public ThemePreference Theme { get; init; } = ThemePreference.System;

    /// <summary>Last window width in device-independent pixels.</summary>
    public double WindowWidth { get; init; } = 1120;

    /// <summary>Last window height in device-independent pixels.</summary>
    public double WindowHeight { get; init; } = 760;

    /// <summary>Whether the window was maximized when last closed.</summary>
    public bool WindowMaximized { get; init; }

    /// <summary>The folder last used in an open/save dialog, when any.</summary>
    public string? LastOpenFolder { get; init; }

    /// <summary>Factory-default settings.</summary>
    public static AppSettings Default { get; } = new();
}

