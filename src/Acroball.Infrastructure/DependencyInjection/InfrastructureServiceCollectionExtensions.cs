using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Acroball.Application.Abstractions;
using Acroball.Application.Jobs;
using Acroball.Infrastructure.Logging;
using Acroball.Infrastructure.Pdf;
using Acroball.Infrastructure.Persistence;
using Acroball.Infrastructure.Updates;

namespace Acroball.Infrastructure.DependencyInjection;

/// <summary>
/// Registers Acroball's infrastructure services. Called from the composition
/// root (Acroball.Desktop); nothing else references this assembly's concretions.
/// </summary>
public static class InfrastructureServiceCollectionExtensions
{
    /// <summary>
    /// Adds paths, logging, settings, recent files and the update stub.
    /// </summary>
    /// <param name="services">The service collection to populate.</param>
    /// <param name="paths">
    /// Data paths to use; defaults to <see cref="AppPaths.CreateDefault"/>.
    /// Injectable for tests.
    /// </param>
    public static IServiceCollection AddAcroballInfrastructure(
        this IServiceCollection services,
        AppPaths? paths = null)
    {
        paths ??= AppPaths.CreateDefault();
        services.AddSingleton(paths);

        services.AddLogging(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Information);
#if DEBUG
            builder.SetMinimumLevel(LogLevel.Debug);
            builder.AddDebug();
#endif
            builder.AddProvider(new FileLoggerProvider(paths.LogsDirectory));
        });

        services.AddSingleton<IAppSettingsService>(sp => JsonSettingsService.Load(
            sp.GetRequiredService<AppPaths>(),
            sp.GetRequiredService<ILogger<JsonSettingsService>>()));

        services.AddSingleton<IRecentFilesService, RecentFilesService>();
        services.AddSingleton<IUpdateService, NullUpdateService>();

        // Stateless, thread-safe; PDFsharp behind the abstraction (ADR-0002).
        services.AddSingleton<IPdfEngine, PdfSharpEngine>();
        services.AddSingleton<IJobExecutor, JobRunner>();

        // Milestone 3 registers IPdfRenderService (PDFium via PDFtoImage).

        return services;
    }
}

