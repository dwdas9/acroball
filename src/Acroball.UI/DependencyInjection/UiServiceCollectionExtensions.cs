using Microsoft.Extensions.DependencyInjection;
using Acroball.Application.Abstractions;
using Acroball.UI.Services;
using Acroball.UI.ViewModels;

namespace Acroball.UI.DependencyInjection;

/// <summary>Registers Acroball's presentation-layer services.</summary>
public static class UiServiceCollectionExtensions
{
    /// <summary>Adds the shell, page factory, pages and theme service.</summary>
    public static IServiceCollection AddAcroballUi(this IServiceCollection services)
    {
        services.AddSingleton<IThemeService, ThemeService>();
        services.AddSingleton<IFileDialogService, AvaloniaFileDialogService>();
        services.AddSingleton<PageFactory>();
        services.AddSingleton<MainWindowViewModel>();

        // Pages are transient: navigating away discards state by design in M1.
        services.AddTransient<HomeViewModel>();
        services.AddTransient<SettingsViewModel>();
        services.AddTransient<MergeViewModel>();
        services.AddTransient<SplitViewModel>();
        services.AddTransient<ExtractViewModel>();
        services.AddTransient<RotateViewModel>();
        services.AddTransient<CompressViewModel>();
        services.AddTransient<MetadataViewModel>();
        services.AddTransient<ProtectViewModel>();

        return services;
    }
}

