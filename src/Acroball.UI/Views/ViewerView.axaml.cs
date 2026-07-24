using Avalonia.Controls;
using Acroball.UI.ViewModels;

namespace Acroball.UI.Views;

/// <summary>
/// Code-behind for the Viewer tool view: drives lazy per-page rendering off
/// the virtualizing panel's container realize/derealize notifications.
/// </summary>
public partial class ViewerView : UserControl
{
    /// <summary>Creates the view.</summary>
    public ViewerView()
    {
        InitializeComponent();
    }

    private ViewerViewModel? ViewModel => DataContext as ViewerViewModel;

    private void OnPageContainerPrepared(object? sender, ContainerPreparedEventArgs e)
    {
        if (e.Container.DataContext is ViewerPageViewModel page)
        {
            _ = ViewModel?.LoadPageImageAsync(page);
        }
    }

    private void OnPageContainerClearing(object? sender, ContainerClearingEventArgs e)
    {
        if (e.Container.DataContext is ViewerPageViewModel page)
        {
            ViewModel?.UnloadPageImage(page);
        }
    }
}
