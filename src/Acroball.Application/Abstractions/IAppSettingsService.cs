using Acroball.Application.Models;

namespace Acroball.Application.Abstractions;

/// <summary>
/// Loads, mutates and persists <see cref="AppSettings"/>.
/// </summary>
/// <remarks>
/// <see cref="Current"/> is read synchronously because settings are loaded once
/// at startup and kept in memory; <see cref="SaveAsync"/> writes atomically.
/// </remarks>
public interface IAppSettingsService
{
    /// <summary>The current settings snapshot.</summary>
    AppSettings Current { get; }

    /// <summary>Raised after <see cref="Update"/> replaces the snapshot.</summary>
    event EventHandler<AppSettings>? Changed;

    /// <summary>
    /// Atomically replaces the snapshot by applying <paramref name="mutate"/>
    /// to the current value. Does not persist; call <see cref="SaveAsync"/>.
    /// </summary>
    void Update(Func<AppSettings, AppSettings> mutate);

    /// <summary>Persists the current snapshot to disk.</summary>
    Task SaveAsync(CancellationToken cancellationToken = default);
}

