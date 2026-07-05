using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Acroball.Application.Abstractions;

namespace Acroball.UI.Views;

/// <summary>
/// The shell window. Persists its size on close and, on macOS, extends
/// content under the title bar so the sidebar reads as one surface with the
/// window chrome (ADR-0003: standard decorations elsewhere in M1).
/// </summary>
public partial class MainWindow : Window
{
    private readonly IAppSettingsService? _settings;

    /// <summary>Parameterless constructor for the XAML previewer only.</summary>
    public MainWindow()
    {
        InitializeComponent();
    }

    /// <summary>Creates the window with access to settings for size persistence.</summary>
    public MainWindow(IAppSettingsService settings)
        : this()
    {
        _settings = settings;

        if (OperatingSystem.IsMacOS())
        {
            ConfigureMacTitleBar();
        }
    }

    private void ConfigureMacTitleBar()
    {
        ExtendClientAreaToDecorationsHint = true;

        // Clear the traffic lights and make the header row drag the window.
        SidebarHeader.Padding = new Thickness(72, 0, 0, 0);
        SidebarHeader.PointerPressed += OnHeaderPointerPressed;
        SidebarHeader.DoubleTapped += OnHeaderDoubleTapped;
    }

    private void OnHeaderPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            BeginMoveDrag(e);
        }
    }

    private void OnHeaderDoubleTapped(object? sender, RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;
    }

    /// <inheritdoc />
    protected override void OnClosing(WindowClosingEventArgs e)
    {
        if (_settings is not null)
        {
            var maximized = WindowState == WindowState.Maximized;
            var width = Width;
            var height = Height;

            _settings.Update(s => maximized
                ? s with { WindowMaximized = true }
                : s with { WindowMaximized = false, WindowWidth = width, WindowHeight = height });

            // Blocking here is deliberate: the write is a tiny atomic file
            // move and the app is quitting; fire-and-forget would race
            // process teardown and lose the size.
            _settings.SaveAsync().GetAwaiter().GetResult();
        }

        base.OnClosing(e);
    }
}

