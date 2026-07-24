using Microsoft.Extensions.DependencyInjection;
using Acroball.UI.Tools;
using Acroball.UI.ViewModels;

namespace Acroball.UI.Services;

/// <summary>Well-known page ids that are not tools.</summary>
public static class PageIds
{
    /// <summary>The home page.</summary>
    public const string Home = "home";

    /// <summary>The settings page.</summary>
    public const string Settings = "settings";
}

/// <summary>
/// Creates page view models by id. Home and Settings resolve through the
/// container (they have dependencies); tool ids resolve to placeholders until
/// their milestone replaces them here.
/// </summary>
public sealed class PageFactory
{
    private readonly IServiceProvider _services;

    /// <summary>Creates the factory over the composed container.</summary>
    public PageFactory(IServiceProvider services)
    {
        _services = services;
    }

    /// <summary>Creates the page for <paramref name="pageId"/>.</summary>
    public PageViewModel Create(string pageId) => pageId switch
    {
        PageIds.Home => _services.GetRequiredService<HomeViewModel>(),
        PageIds.Settings => _services.GetRequiredService<SettingsViewModel>(),
        "viewer" => _services.GetRequiredService<ViewerViewModel>(),
        "merge" => _services.GetRequiredService<MergeViewModel>(),
        "split" => _services.GetRequiredService<SplitViewModel>(),
        "extract" => _services.GetRequiredService<ExtractViewModel>(),
        "organize" => _services.GetRequiredService<OrganizeViewModel>(),
        "rotate" => _services.GetRequiredService<RotateViewModel>(),
        "compress" => _services.GetRequiredService<CompressViewModel>(),
        "metadata" => _services.GetRequiredService<MetadataViewModel>(),
        "protect" => _services.GetRequiredService<ProtectViewModel>(),
        "fill-form" => _services.GetRequiredService<FormViewModel>(),
        _ => new ToolPlaceholderViewModel(ToolCatalog.GetById(pageId)),
    };
}

