using Avalonia.Controls;
using Acroball.UI.ViewModels;

namespace Acroball.UI.Views;

/// <summary>
/// Code-behind for the Viewer tool view: drives lazy per-page rendering off
/// the virtualizing panel's container realize/derealize notifications, and
/// bridges bookmark clicks to scrolling the page list.
/// </summary>
public partial class ViewerView : UserControl
{
    private ViewerViewModel? _subscribedViewModel;

    /// <summary>Creates the view.</summary>
    public ViewerView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private ViewerViewModel? ViewModel => DataContext as ViewerViewModel;

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_subscribedViewModel is not null)
        {
            _subscribedViewModel.ScrollToPageRequested -= OnScrollToPageRequested;
        }

        _subscribedViewModel = ViewModel;

        if (_subscribedViewModel is not null)
        {
            _subscribedViewModel.ScrollToPageRequested += OnScrollToPageRequested;
        }
    }

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

    private void OnOutlineSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (e.AddedItems.Count > 0 && e.AddedItems[0] is OutlineNodeViewModel { DestinationPageNumber: { } pageNumber })
        {
            ViewModel?.RequestScrollToPage(pageNumber);
        }
    }

    private void OnScrollToPageRequested(int pageNumber)
    {
        var page = ViewModel?.Pages.FirstOrDefault(p => p.PageNumber == pageNumber);
        if (page is not null)
        {
            PagesList.ScrollIntoView(page);
        }
    }
}
