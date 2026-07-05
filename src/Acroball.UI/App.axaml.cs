using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using Acroball.Application.Abstractions;
using Acroball.UI.ViewModels;
using Acroball.UI.Views;

namespace Acroball.UI;

/// <summary>
/// The Avalonia application. Owns no services itself; the composition root
/// (Acroball.Desktop) builds the container and hands it in.
/// </summary>
public partial class App : Avalonia.Application
{
    private readonly IServiceProvider? _services;

    /// <summary>Parameterless constructor for the XAML previewer only.</summary>
    public App()
    {
    }

    /// <summary>Creates the application with the composed service provider.</summary>
    public App(IServiceProvider services)
    {
        _services = services;
    }

    /// <inheritdoc />
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    /// <inheritdoc />
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop && _services is not null)
        {
            var settingsService = _services.GetRequiredService<IAppSettingsService>();
            var settings = settingsService.Current;

            // Apply the persisted theme before any window renders.
            _services.GetRequiredService<IThemeService>().Apply(settings.Theme);

            var window = new MainWindow(settingsService)
            {
                DataContext = _services.GetRequiredService<MainWindowViewModel>(),
                Width = settings.WindowWidth,
                Height = settings.WindowHeight,
            };

            if (settings.WindowMaximized)
            {
                window.WindowState = Avalonia.Controls.WindowState.Maximized;
            }

            desktop.MainWindow = window;
        }

        base.OnFrameworkInitializationCompleted();
    }
}

