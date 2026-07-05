using Acroball.Application.Abstractions;

namespace Acroball.Infrastructure.Updates;

/// <summary>
/// Placeholder <see cref="IUpdateService"/> for builds that cannot
/// self-update. Replaced by the Velopack implementation in the packaging
/// milestone (see ADR-0008).
/// </summary>
public sealed class NullUpdateService : IUpdateService
{
    /// <inheritdoc />
    public bool IsSupported => false;

    /// <inheritdoc />
    public Task<UpdateInfo?> CheckForUpdatesAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<UpdateInfo?>(null);
}

