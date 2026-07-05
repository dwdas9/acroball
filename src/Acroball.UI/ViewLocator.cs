using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Acroball.UI.ViewModels;
using Acroball.UI.Views;

namespace Acroball.UI;

/// <summary>
/// Maps page view models to views through an explicit table. Explicit (rather
/// than reflection over type names) so trimming can never break navigation
/// and so the mapping is greppable. See ADR-0003.
/// </summary>
public sealed class ViewLocator : IDataTemplate
{
    private static readonly Dictionary<Type, Func<Control>> Registry = new()
    {
        [typeof(HomeViewModel)] = () => new HomeView(),
        [typeof(SettingsViewModel)] = () => new SettingsView(),
        [typeof(MergeViewModel)] = () => new MergeView(),
        [typeof(ToolPlaceholderViewModel)] = () => new ToolPlaceholderView(),
    };

    /// <inheritdoc />
    public Control Build(object? data)
    {
        if (data is not null && Registry.TryGetValue(data.GetType(), out var factory))
        {
            return factory();
        }

        return new TextBlock { Text = $"No view registered for {data?.GetType().Name ?? "null"}" };
    }

    /// <inheritdoc />
    public bool Match(object? data) => data is PageViewModel;
}

