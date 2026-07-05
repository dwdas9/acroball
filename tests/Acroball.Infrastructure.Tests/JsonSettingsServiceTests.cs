using Microsoft.Extensions.Logging.Abstractions;
using Acroball.Application.Models;
using Acroball.Infrastructure.Persistence;
using Xunit;

namespace Acroball.Infrastructure.Tests;

public class JsonSettingsServiceTests : IDisposable
{
    private readonly string _tempDir;

    public JsonSettingsServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "Acroball-tests-" + Guid.NewGuid().ToString("N"));
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_tempDir, recursive: true);
        }
        catch (IOException)
        {
            // Best-effort cleanup.
        }
    }

    private AppPaths CreatePaths() => new(_tempDir);

    [Fact]
    public void Load_returns_defaults_when_file_missing()
    {
        var service = JsonSettingsService.Load(CreatePaths(), NullLogger<JsonSettingsService>.Instance);

        Assert.Equal(AppSettings.Default, service.Current);
    }

    [Fact]
    public async Task Update_and_save_round_trips_through_a_fresh_load()
    {
        var paths = CreatePaths();
        var service = JsonSettingsService.Load(paths, NullLogger<JsonSettingsService>.Instance);

        service.Update(s => s with { Theme = ThemePreference.Dark, WindowWidth = 999 });
        await service.SaveAsync(TestContext.Current.CancellationToken);

        var reloaded = JsonSettingsService.Load(paths, NullLogger<JsonSettingsService>.Instance);
        Assert.Equal(ThemePreference.Dark, reloaded.Current.Theme);
        Assert.Equal(999, reloaded.Current.WindowWidth);
    }

    [Fact]
    public void Load_falls_back_to_defaults_on_corrupted_file()
    {
        var paths = CreatePaths();
        File.WriteAllText(paths.SettingsFilePath, "{ this is not json !!!");

        var service = JsonSettingsService.Load(paths, NullLogger<JsonSettingsService>.Instance);

        Assert.Equal(AppSettings.Default, service.Current);
    }

    [Fact]
    public void Update_raises_changed_with_new_snapshot()
    {
        var service = JsonSettingsService.Load(CreatePaths(), NullLogger<JsonSettingsService>.Instance);
        AppSettings? observed = null;
        service.Changed += (_, s) => observed = s;

        service.Update(s => s with { Theme = ThemePreference.Light });

        Assert.NotNull(observed);
        Assert.Equal(ThemePreference.Light, observed!.Theme);
    }
}

