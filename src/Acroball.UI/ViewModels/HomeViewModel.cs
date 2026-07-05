using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Acroball.UI.Messages;
using Acroball.UI.Tools;

namespace Acroball.UI.ViewModels;

/// <summary>Home page: the tool catalog as a card grid.</summary>
public sealed partial class HomeViewModel : PageViewModel
{
    /// <inheritdoc />
    public override string Title => "Home";

    /// <inheritdoc />
    public override string IconKey => "Home";

    /// <summary>Tools shown as cards.</summary>
    public IReadOnlyList<ToolDefinition> Tools { get; } = ToolCatalog.All;

    [RelayCommand]
    internal void OpenTool(ToolDefinition tool)
        => WeakReferenceMessenger.Default.Send(new NavigateToToolMessage(tool.Id));
}

