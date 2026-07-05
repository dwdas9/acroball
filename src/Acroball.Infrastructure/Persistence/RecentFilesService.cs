using System.Text.Json;
using Microsoft.Extensions.Logging;
using Acroball.Application.Abstractions;
using Acroball.Application.Models;

namespace Acroball.Infrastructure.Persistence;

/// <summary>
/// <see cref="IRecentFilesService"/> backed by a JSON file, capped to
/// <see cref="Capacity"/> entries, most recently opened first. Writes are
/// atomic (temp file + move), mirroring <see cref="JsonSettingsService"/>.
/// </summary>
public sealed class RecentFilesService : IRecentFilesService
{
    /// <summary>Maximum number of entries kept.</summary>
    public const int Capacity = 12;

    private readonly AppPaths _paths;
    private readonly ILogger<RecentFilesService> _logger;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private List<RecentFileEntry>? _entries;

    /// <summary>Creates the service. The list is loaded lazily on first use.</summary>
    public RecentFilesService(AppPaths paths, ILogger<RecentFilesService> logger)
    {
        _paths = paths;
        _logger = logger;
    }

    /// <inheritdoc />
    public IReadOnlyList<RecentFileEntry> GetAll()
    {
        _lock.Wait();
        try
        {
            return EnsureLoaded().ToArray();
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc />
    public async Task TouchAsync(string filePath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var entries = EnsureLoaded();
            entries.RemoveAll(e => PathsEqual(e.FilePath, filePath));
            entries.Insert(0, new RecentFileEntry(filePath, DateTimeOffset.UtcNow));

            if (entries.Count > Capacity)
            {
                entries.RemoveRange(Capacity, entries.Count - Capacity);
            }

            await PersistAsync(entries, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc />
    public async Task RemoveAsync(string filePath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var entries = EnsureLoaded();
            if (entries.RemoveAll(e => PathsEqual(e.FilePath, filePath)) > 0)
            {
                await PersistAsync(entries, cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc />
    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var entries = EnsureLoaded();
            entries.Clear();
            await PersistAsync(entries, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _lock.Release();
        }
    }

    private List<RecentFileEntry> EnsureLoaded()
    {
        if (_entries is not null)
        {
            return _entries;
        }

        _entries = new List<RecentFileEntry>();

        try
        {
            if (File.Exists(_paths.RecentFilesFilePath))
            {
                var json = File.ReadAllText(_paths.RecentFilesFilePath);
                var loaded = JsonSerializer.Deserialize(json, AcroballJsonContext.Default.ListRecentFileEntry);
                if (loaded is not null)
                {
                    _entries = loaded;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Recent-files list was unreadable; starting empty. Path: {Path}", _paths.RecentFilesFilePath);
        }

        return _entries;
    }

    private async Task PersistAsync(List<RecentFileEntry> entries, CancellationToken cancellationToken)
    {
        try
        {
            var json = JsonSerializer.Serialize(entries, AcroballJsonContext.Default.ListRecentFileEntry);
            var tempPath = _paths.RecentFilesFilePath + ".tmp";
            await File.WriteAllTextAsync(tempPath, json, cancellationToken).ConfigureAwait(false);
            File.Move(tempPath, _paths.RecentFilesFilePath, overwrite: true);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save recent-files list to {Path}", _paths.RecentFilesFilePath);
        }
    }

    private static bool PathsEqual(string a, string b)
        => string.Equals(
            Path.GetFullPath(a),
            Path.GetFullPath(b),
            OperatingSystem.IsLinux() ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase);
}

