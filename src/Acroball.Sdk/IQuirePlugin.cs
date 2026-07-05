using Microsoft.Extensions.DependencyInjection;

namespace Acroball.Sdk;

/// <summary>
/// Entry point implemented by a Acroball plugin assembly. Plugins are discovered
/// from the plugins directory and loaded into isolated, collectible
/// <see cref="System.Runtime.Loader.AssemblyLoadContext"/> instances (see ADR-0005).
/// </summary>
/// <remarks>
/// This SDK assembly is intentionally tiny and stable: plugins reference it
/// (plus <c>Microsoft.Extensions.DependencyInjection.Abstractions</c>) and
/// nothing else from the host. Richer contribution points (tool pages, page
/// context actions) will be added here as the plugin milestone lands.
/// </remarks>
public interface IAcroballPlugin
{
    /// <summary>Stable machine identifier, e.g. <c>"com.example.watermark"</c>.</summary>
    string Id { get; }

    /// <summary>Human-readable plugin name.</summary>
    string DisplayName { get; }

    /// <summary>One-sentence description shown in the plugin manager.</summary>
    string Description { get; }

    /// <summary>Plugin version.</summary>
    Version Version { get; }

    /// <summary>
    /// Registers the plugin's services into the host container during startup.
    /// </summary>
    void ConfigureServices(IServiceCollection services);
}

