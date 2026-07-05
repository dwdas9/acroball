using System.Reflection;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using Acroball.Application.Abstractions;
using Acroball.Application.Models;

namespace Acroball.UI.ViewModels;

/// <summary>Settings page: appearance and about.</summary>
public sealed partial class SettingsViewModel : PageViewModel
{
    private readonly IThemeService _themeService;
    private readonly IAppSettingsService _settings;
    private readonly ILogger<SettingsViewModel> _logger;
    private readonly bool _initialized;

    /// <inheritdoc />
    public override string Title => "Settings";

    /// <inheritdoc />
    public override string IconKey => "Settings";

    /// <summary>The selected theme; applies and persists on change.</summary>
    [ObservableProperty]
    private ThemePreference _selectedTheme;

    /// <summary>Version string shown in the About section.</summary>
    public string AppVersion { get; }

    /// <summary>Creates the page from current settings.</summary>
    public SettingsViewModel(
        IThemeService themeService,
        IAppSettingsService settings,
        ILogger<SettingsViewModel> logger)
    {
        _themeService = themeService;
        _settings = settings;
        _logger = logger;

        _selectedTheme = settings.Current.Theme;
        AppVersion = ResolveVersion();
        _initialized = true;
    }

    partial void OnSelectedThemeChanged(ThemePreference value)
    {
        if (!_initialized)
        {
            return;
        }

        _themeService.Apply(value);
        _ = PersistAsync(value);
    }

    private async Task PersistAsync(ThemePreference value)
    {
        try
        {
            _settings.Update(s => s with { Theme = value });
            await _settings.SaveAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist theme change");
        }
    }

    private static string ResolveVersion()
    {
        var informational = typeof(SettingsViewModel).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;

        if (string.IsNullOrWhiteSpace(informational))
        {
            return "0.1.0";
        }

        var plus = informational.IndexOf('+');
        return plus > 0 ? informational[..plus] : informational;
    }
}

