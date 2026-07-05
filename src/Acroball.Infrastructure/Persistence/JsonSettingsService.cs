using System.Text.Json;
using Microsoft.Extensions.Logging;
using Acroball.Application.Abstractions;
using Acroball.Application.Models;

namespace Acroball.Infrastructure.Persistence;

/// <summary>
/// <see cref="IAppSettingsService"/> backed by a JSON file. Loads once at
/// startup, keeps the snapshot in memory, and writes atomically
/// (temp file + move) so a crash mid-write can never corrupt settings.
/// </summary>
public sealed class JsonSettingsService : IAppSettingsService
{
    private readonly AppPaths _paths;
    private readonly ILogger<JsonSettingsService> _logger;
    private readonly SemaphoreSlim _saveLock = new(1, 1);
    private readonly object _stateLock = new();
    private AppSettings _current;

    private JsonSettingsService(AppPaths paths, ILogger<JsonSettingsService> logger, AppSettings initial)
    {
        _paths = paths;
        _logger = logger;
        _current = initial;
    }

    /// <inheritdoc />
    public AppSettings Current
    {
        get
        {
            lock (_stateLock)
            {
                return _current;
            }
        }
    }

    /// <inheritdoc />
    public event EventHandler<AppSettings>? Changed;

    /// <summary>
    /// Loads settings from disk. Missing or unreadable files fall back to
    /// <see cref="AppSettings.Default"/> â€” settings must never block startup.
    /// </summary>
    public static JsonSettingsService Load(AppPaths paths, ILogger<JsonSettingsService> logger)
    {
        var settings = AppSettings.Default;

        try
        {
            if (File.Exists(paths.SettingsFilePath))
            {
                var json = File.ReadAllText(paths.SettingsFilePath);
                settings = JsonSerializer.Deserialize(json, AcroballJsonContext.Default.AppSettings)
                           ?? AppSettings.Default;
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Settings file was unreadable; starting with defaults. Path: {Path}", paths.SettingsFilePath);
            settings = AppSettings.Default;
        }

        return new JsonSettingsService(paths, logger, settings);
    }

    /// <inheritdoc />
    public void Update(Func<AppSettings, AppSettings> mutate)
    {
        ArgumentNullException.ThrowIfNull(mutate);

        AppSettings updated;
        lock (_stateLock)
        {
            updated = mutate(_current);
            _current = updated;
        }

        Changed?.Invoke(this, updated);
    }

    /// <inheritdoc />
    public async Task SaveAsync(CancellationToken cancellationToken = default)
    {
        await _saveLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var json = JsonSerializer.Serialize(Current, AcroballJsonContext.Default.AppSettings);
            var tempPath = _paths.SettingsFilePath + ".tmp";
            await File.WriteAllTextAsync(tempPath, json, cancellationToken).ConfigureAwait(false);
            File.Move(tempPath, _paths.SettingsFilePath, overwrite: true);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Losing a settings write is annoying, not fatal; log and move on.
            _logger.LogError(ex, "Failed to save settings to {Path}", _paths.SettingsFilePath);
        }
        finally
        {
            _saveLock.Release();
        }
    }
}

