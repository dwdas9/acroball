using Microsoft.Extensions.Logging.Abstractions;
using Acroball.Infrastructure.Persistence;
using Xunit;

namespace Acroball.Infrastructure.Tests;

public class RecentFilesServiceTests : IDisposable
{
    private readonly string _tempDir;

    public RecentFilesServiceTests()
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

    private RecentFilesService CreateService()
        => new(new AppPaths(_tempDir), NullLogger<RecentFilesService>.Instance);

    private string FakePath(int i) => Path.Combine(_tempDir, $"file-{i}.pdf");

    [Fact]
    public async Task Touch_puts_most_recent_first()
    {
        var ct = TestContext.Current.CancellationToken;
        var service = CreateService();
        await service.TouchAsync(FakePath(1), ct);
        await service.TouchAsync(FakePath(2), ct);

        var all = service.GetAll();
        Assert.Equal(FakePath(2), all[0].FilePath);
        Assert.Equal(FakePath(1), all[1].FilePath);
    }

    [Fact]
    public async Task Touching_existing_entry_moves_it_to_front_without_duplicating()
    {
        var ct = TestContext.Current.CancellationToken;
        var service = CreateService();
        await service.TouchAsync(FakePath(1), ct);
        await service.TouchAsync(FakePath(2), ct);
        await service.TouchAsync(FakePath(1), ct);

        var all = service.GetAll();
        Assert.Equal(2, all.Count);
        Assert.Equal(FakePath(1), all[0].FilePath);
    }

    [Fact]
    public async Task List_is_capped_at_capacity()
    {
        var ct = TestContext.Current.CancellationToken;
        var service = CreateService();
        for (var i = 1; i <= RecentFilesService.Capacity + 3; i++)
        {
            await service.TouchAsync(FakePath(i), ct);
        }

        var all = service.GetAll();
        Assert.Equal(RecentFilesService.Capacity, all.Count);
        Assert.Equal(FakePath(RecentFilesService.Capacity + 3), all[0].FilePath);
    }

    [Fact]
    public async Task List_round_trips_through_a_fresh_instance()
    {
        var ct = TestContext.Current.CancellationToken;
        var first = CreateService();
        await first.TouchAsync(FakePath(1), ct);
        await first.TouchAsync(FakePath(2), ct);

        var second = CreateService();
        var all = second.GetAll();
        Assert.Equal(2, all.Count);
        Assert.Equal(FakePath(2), all[0].FilePath);
    }

    [Fact]
    public async Task Remove_and_clear_persist()
    {
        var ct = TestContext.Current.CancellationToken;
        var service = CreateService();
        await service.TouchAsync(FakePath(1), ct);
        await service.TouchAsync(FakePath(2), ct);

        await service.RemoveAsync(FakePath(1), ct);
        Assert.Single(service.GetAll());

        await service.ClearAsync(ct);
        Assert.Empty(service.GetAll());
    }
}

