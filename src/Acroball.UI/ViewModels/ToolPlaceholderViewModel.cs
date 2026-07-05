using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Acroball.UI.Messages;
using Acroball.UI.Services;
using Acroball.UI.Tools;

namespace Acroball.UI.ViewModels;

/// <summary>
/// Stand-in page for tools that ship in later milestones; shows the tool's
/// identity and when it lands.
/// </summary>
public sealed partial class ToolPlaceholderViewModel : PageViewModel
{
    private readonly ToolDefinition _tool;

    /// <summary>Creates the placeholder for <paramref name="tool"/>.</summary>
    public ToolPlaceholderViewModel(ToolDefinition tool)
    {
        _tool = tool;
    }

    /// <inheritdoc />
    public override string Title => _tool.Label;

    /// <inheritdoc />
    public override string IconKey => _tool.IconKey;

    /// <summary>One-line description of the tool.</summary>
    public string Description => _tool.Description;

    /// <summary>Milestone label, e.g. <c>"Milestone 2"</c>.</summary>
    public string MilestoneTag => _tool.Milestone switch
    {
        "M2" => "Milestone 2",
        "M3" => "Milestone 3",
        "M4" => "Milestone 4",
        _ => _tool.Milestone,
    };

    [RelayCommand]
    internal void GoHome()
        => WeakReferenceMessenger.Default.Send(new NavigateToToolMessage(PageIds.Home));
}

