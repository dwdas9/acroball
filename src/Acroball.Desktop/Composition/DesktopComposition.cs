using System.Runtime.Versioning;
using Microsoft.Extensions.DependencyInjection;
using Acroball.Infrastructure.DependencyInjection;
using Acroball.UI.DependencyInjection;

namespace Acroball.Desktop.Composition;

/// <summary>
/// The composition root: the only place that sees every layer's registrations
/// (ADR-0001).
/// </summary>
public static class DesktopComposition
{
    /// <summary>Builds the fully validated container.</summary>
    [SupportedOSPlatform("windows")]
    [SupportedOSPlatform("macos")]
    [SupportedOSPlatform("linux")]
    public static ServiceProvider BuildServiceProvider()
    {
        var services = new ServiceCollection();

        services.AddAcroballInfrastructure();
        services.AddAcroballUi();

        return services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true,
        });
    }
}

