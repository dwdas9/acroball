using Microsoft.Extensions.Logging.Abstractions;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using Acroball.Application.Operations;
using Acroball.Domain;
using Acroball.Domain.Exceptions;
using Acroball.Infrastructure.Pdf;
using Xunit;

namespace Acroball.Infrastructure.Tests;

/// <summary>
/// Integration tests over real PDFs on disk. Fixtures encode page identity in
/// page geometry (page n of a fixture is 100+n points wide), so assertions
/// never need text rendering or fonts â€” which keeps the suite green on bare
/// CI runners with no system fonts.
/// </summary>
public class PdfSharpEngineTests : IDisposable
{
    private readonly string _dir;
    private readonly PdfSharpEngine _engine = new(NullLogger<PdfSharpEngine>.Instance);

    public PdfSharpEngineTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "Acroball-engine-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_dir, recursive: true);
        }
        catch (IOException)
        {
            // Best-effort cleanup.
        }
    }

    // ======================== fixtures ========================

    private string P(string name) => Path.Combine(_dir, name);

    /// <summary>Creates a PDF whose page n is (100+n) x 200 points.</summary>
    private string CreateFixture(string name, int pageCount, Action<PdfDocument>? customize = null)
    {
        var path = P(name);
        using var document = new PdfDocument();
        for (var n = 1; n <= pageCount; n++)
        {
            var page = document.AddPage();
            page.Width = XUnit.FromPoint(100 + n);
            page.Height = XUnit.FromPoint(200);
        }

        customize?.Invoke(document);
        document.Save(path);
        return path;
    }

    private string CreateEncryptedFixture(string name, int pageCount, string userPassword)
        => CreateFixture(name, pageCount, doc => doc.SecuritySettings.UserPassword = userPassword);

    private async Task<double[]> PageWidthsOf(string path, string? password = null)
    {
        var pages = await _engine.GetPagesAsync(path, password, TestContext.Current.CancellationToken);
        return pages.Select(p => p.WidthPoints).ToArray();
    }

    private static void AssertWidths(double[] expected, double[] actual)
    {
        Assert.Equal(expected.Length, actual.Length);
        for (var i = 0; i < expected.Length; i++)
        {
            Assert.Equal(expected[i], actual[i], 1);
        }
    }

    /// <summary>Synchronous progress collector: no SynchronizationContext timing games.</summary>
    private sealed class ProgressCollector : IProgress<OperationProgress>
    {
        private readonly List<OperationProgress> _reports = [];

        public IReadOnlyList<OperationProgress> Reports
        {
            get
            {
                lock (_reports)
                {
                    return _reports.ToArray();
                }
            }
        }

        public void Report(OperationProgress value)
        {
            lock (_reports)
            {
                _reports.Add(value);
            }
        }
    }

    // ======================== inspect / pages ========================

    [Fact]
    public async Task Inspect_reads_page_count_size_and_version()
    {
        var path = CreateFixture("a.pdf", 3);

        var info = await _engine.InspectAsync(path, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(3, info.PageCount);
        Assert.True(info.FileSizeBytes > 0);
        Assert.False(info.IsEncrypted);
        Assert.NotNull(info.PdfVersion);
    }

    [Fact]
    public async Task GetPages_returns_geometry_and_rotation()
    {
        var path = CreateFixture("a.pdf", 2, doc => doc.Pages[1].Rotate = 90);

        var pages = await _engine.GetPagesAsync(path, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(2, pages.Count);
        Assert.Equal(101, pages[0].WidthPoints, 1);
        Assert.Equal(Rotation.None, pages[0].Rotation);
        Assert.Equal(Rotation.Clockwise90, pages[1].Rotation);
        Assert.True(pages[1].IsLandscape); // 102x200 rotated 90Â° presents landscape
    }

    // ======================== merge ========================

    [Fact]
    public async Task Merge_combines_files_in_order()
    {
        var a = CreateFixture("a.pdf", 3);
        var b = CreateFixture("b.pdf", 2);
        var output = P("merged.pdf");

        await _engine.MergeAsync(
            new MergeRequest([a, b], output),
            cancellationToken: TestContext.Current.CancellationToken);

        AssertWidths([101, 102, 103, 101, 102], await PageWidthsOf(output));
    }

    [Fact]
    public async Task Merge_reports_monotonic_progress_ending_at_one()
    {
        var a = CreateFixture("a.pdf", 2);
        var b = CreateFixture("b.pdf", 2);
        var progress = new ProgressCollector();

        await _engine.MergeAsync(
            new MergeRequest([a, b], P("merged.pdf")),
            progress,
            TestContext.Current.CancellationToken);

        var fractions = progress.Reports.Select(r => r.Fraction).ToArray();
        Assert.True(fractions.Length >= 4);
        Assert.True(fractions.SequenceEqual(fractions.OrderBy(f => f)), "progress went backwards");
        Assert.Equal(1.0, fractions[^1]);
    }

    [Fact]
    public async Task Merge_with_precancelled_token_throws_and_writes_nothing()
    {
        var a = CreateFixture("a.pdf", 2);
        var output = P("merged.pdf");
        var cancelled = new CancellationToken(canceled: true);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => _engine.MergeAsync(new MergeRequest([a], output), cancellationToken: cancelled));

        Assert.False(File.Exists(output));
    }

    [Fact]
    public async Task Merge_rejects_empty_input_list()
        => await Assert.ThrowsAsync<PdfOperationException>(
            () => _engine.MergeAsync(
                new MergeRequest([], P("out.pdf")),
                cancellationToken: TestContext.Current.CancellationToken));

    // ======================== split ========================

    [Fact]
    public async Task Split_writes_one_file_per_range_with_template()
    {
        var source = CreateFixture("doc.pdf", 5);
        var outDir = Path.Combine(_dir, "parts");

        await _engine.SplitAsync(
            new SplitRequest(source, outDir, [new PageRange(1, 2), new PageRange(4, 5)]),
            cancellationToken: TestContext.Current.CancellationToken);

        AssertWidths([101, 102], await PageWidthsOf(Path.Combine(outDir, "doc-1.pdf")));
        AssertWidths([104, 105], await PageWidthsOf(Path.Combine(outDir, "doc-2.pdf")));
    }

    [Fact]
    public async Task Split_range_beyond_last_page_throws_before_writing()
    {
        var source = CreateFixture("doc.pdf", 3);
        var outDir = Path.Combine(_dir, "parts");

        await Assert.ThrowsAsync<PdfOperationException>(
            () => _engine.SplitAsync(
                new SplitRequest(source, outDir, [new PageRange(2, 9)]),
                cancellationToken: TestContext.Current.CancellationToken));

        Assert.False(Directory.Exists(outDir) && Directory.EnumerateFiles(outDir).Any());
    }

    // ======================== extract ========================

    [Fact]
    public async Task Extract_preserves_user_order_and_duplicates()
    {
        var source = CreateFixture("doc.pdf", 4);
        var output = P("extracted.pdf");

        await _engine.ExtractPagesAsync(
            new ExtractPagesRequest(
                source,
                output,
                [PageRange.Single(3), PageRange.Single(1), PageRange.Single(1)]),
            cancellationToken: TestContext.Current.CancellationToken);

        AssertWidths([103, 101, 101], await PageWidthsOf(output));
    }

    // ======================== rotate ========================

    [Fact]
    public async Task Rotate_adds_delta_only_to_selected_pages()
    {
        // Page 2 already carries a stored rotation of 90Â°.
        var source = CreateFixture("doc.pdf", 3, doc => doc.Pages[1].Rotate = 90);
        var output = P("rotated.pdf");

        await _engine.RotatePagesAsync(
            new RotatePagesRequest(source, output, [new PageRange(2, 3)], Rotation.Clockwise90),
            cancellationToken: TestContext.Current.CancellationToken);

        var pages = await _engine.GetPagesAsync(output, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(Rotation.None, pages[0].Rotation);        // untouched
        Assert.Equal(Rotation.Rotate180, pages[1].Rotation);   // 90 + 90
        Assert.Equal(Rotation.Clockwise90, pages[2].Rotation); // 0 + 90
    }

    [Fact]
    public async Task Rotate_by_zero_is_rejected()
    {
        var source = CreateFixture("doc.pdf", 1);

        await Assert.ThrowsAsync<PdfOperationException>(
            () => _engine.RotatePagesAsync(
                new RotatePagesRequest(source, P("out.pdf"), [PageRange.Single(1)], Rotation.None),
                cancellationToken: TestContext.Current.CancellationToken));
    }

    // ======================== metadata ========================

    [Fact]
    public async Task UpdateMetadata_overwrites_provided_fields_and_keeps_the_rest()
    {
        var source = CreateFixture("doc.pdf", 1, doc =>
        {
            doc.Info.Title = "Old title";
            doc.Info.Subject = "Keep me";
        });
        var output = P("meta.pdf");

        await _engine.UpdateMetadataAsync(
            new UpdateMetadataRequest(
                source,
                output,
                new DocumentMetadata(Title: "New title", Author: "D Das")),
            cancellationToken: TestContext.Current.CancellationToken);

        var info = await _engine.InspectAsync(output, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal("New title", info.Metadata.Title);
        Assert.Equal("D Das", info.Metadata.Author);
        Assert.Equal("Keep me", info.Metadata.Subject);
    }

    // ======================== passwords / corruption ========================

    [Fact]
    public async Task Encrypted_file_with_correct_password_works_and_reports_encrypted()
    {
        var source = CreateEncryptedFixture("locked.pdf", 2, "hunter2");

        var info = await _engine.InspectAsync(source, "hunter2", TestContext.Current.CancellationToken);

        Assert.True(info.IsEncrypted);
        Assert.Equal(2, info.PageCount);
    }

    [Fact]
    public async Task Encrypted_file_with_wrong_password_throws_invalid_password()
    {
        var source = CreateEncryptedFixture("locked.pdf", 1, "hunter2");

        var ex = await Assert.ThrowsAsync<InvalidPdfPasswordException>(
            () => _engine.GetPagesAsync(source, "wrong", TestContext.Current.CancellationToken));

        Assert.Equal(source, ex.FilePath);
    }

    [Fact]
    public async Task Encrypted_file_with_no_password_throws_invalid_password()
    {
        var source = CreateEncryptedFixture("locked.pdf", 1, "hunter2");

        await Assert.ThrowsAsync<InvalidPdfPasswordException>(
            () => _engine.InspectAsync(source, cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Garbage_file_throws_corrupt()
    {
        var path = P("garbage.pdf");
        await File.WriteAllBytesAsync(path, "this is definitely not a pdf"u8.ToArray(), TestContext.Current.CancellationToken);

        var ex = await Assert.ThrowsAsync<CorruptPdfException>(
            () => _engine.InspectAsync(path, cancellationToken: TestContext.Current.CancellationToken));

        Assert.Equal(path, ex.FilePath);
    }

    [Fact]
    public async Task Missing_file_throws_file_not_found()
        => await Assert.ThrowsAsync<FileNotFoundException>(
            () => _engine.InspectAsync(P("nope.pdf"), cancellationToken: TestContext.Current.CancellationToken));
}

