using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Acroball.UI.Messages;
using Acroball.UI.Services;
using Acroball.UI.Tools;

namespace Acroball.UI.ViewModels;

/// <summary>
/// Shell view model: owns the sidebar navigation state and the current page.
/// </summary>
public sealed partial class MainWindowViewModel : ViewModelBase
{
    private readonly PageFactory _pageFactory;
    private bool _syncingSelection;

    /// <summary>The page currently shown in the content area.</summary>
    [ObservableProperty]
    private PageViewModel? _currentPage;

    /// <summary>The selected sidebar item; null while Settings is shown.</summary>
    [ObservableProperty]
    private NavItem? _selectedNavItem;

    /// <summary>Whether the Settings page is active (drives the button's style).</summary>
    [ObservableProperty]
    private bool _isSettingsActive;

    /// <summary>Sidebar entries: Home followed by the tool catalog.</summary>
    public IReadOnlyList<NavItem> NavItems { get; }

    /// <summary>Creates the shell and navigates to Home.</summary>
    public MainWindowViewModel(PageFactory pageFactory)
    {
        _pageFactory = pageFactory;

        var items = new List<NavItem> { new(PageIds.Home, "Home", "Home") };
        items.AddRange(ToolCatalog.All.Select(t => new NavItem(t.Id, t.Label, t.IconKey)));
        NavItems = items;

        WeakReferenceMessenger.Default.Register<MainWindowViewModel, NavigateToToolMessage>(
            this,
            static (recipient, message) => recipient.NavigateTo(message.ToolId));

        NavigateTo(PageIds.Home);
    }

    /// <summary>Navigates to a page by id and syncs the sidebar selection.</summary>
    public void NavigateTo(string pageId)
    {
        CurrentPage = _pageFactory.Create(pageId);
        IsSettingsActive = pageId == PageIds.Settings;

        _syncingSelection = true;
        try
        {
            SelectedNavItem = NavItems.FirstOrDefault(item => item.Id == pageId);
        }
        finally
        {
            _syncingSelection = false;
        }
    }

    partial void OnSelectedNavItemChanged(NavItem? value)
    {
        if (_syncingSelection || value is null)
        {
            return;
        }

        NavigateTo(value.Id);
    }

    [RelayCommand]
    internal void Navigate(string pageId) => NavigateTo(pageId);

    [RelayCommand]
    internal void OpenSettings() => NavigateTo(PageIds.Settings);
}

