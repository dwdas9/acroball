using Avalonia;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Acroball.Desktop.Composition;
using Acroball.UI;

namespace Acroball.Desktop;

/// <summary>Application entry point and Avalonia bootstrap.</summary>
public static class Program
{
    private static ServiceProvider? _services;

    /// <summary>Main entry point. Composes services, wires crash logging, runs the app.</summary>
    [STAThread]
    public static void Main(string[] args)
    {
        _services = DesktopComposition.BuildServiceProvider();

        var logger = _services.GetRequiredService<ILoggerFactory>().CreateLogger("Acroball.Startup");
        logger.LogInformation("Acroball starting");

        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            logger.LogCritical(e.ExceptionObject as Exception, "Unhandled AppDomain exception");

        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            logger.LogError(e.Exception, "Unobserved task exception");
            e.SetObserved();
        };

        try
        {
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex, "Fatal startup or run failure");
            throw;
        }
        finally
        {
            logger.LogInformation("Acroball exiting");
            _services.Dispose();
        }
    }

    /// <summary>
    /// Avalonia bootstrap. Also invoked by the XAML previewer, which skips
    /// <see cref="Main"/> â€” hence the fallback composition.
    /// </summary>
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure(() => new App(_services ?? DesktopComposition.BuildServiceProvider()))
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}

