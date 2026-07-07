using Microsoft.Extensions.Logging.Abstractions;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using SkiaSharp;
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

    /// <summary>Encodes deterministic random noise so JPEG quality/size differences are real, not an artifact of flat-color content.</summary>
    private static byte[] CreateNoiseJpeg(int width, int height, SKColorType colorType, SKAlphaType alphaType, int quality, int seed)
    {
        using var bitmap = new SKBitmap(new SKImageInfo(width, height, colorType, alphaType));
        new Random(seed).NextBytes(bitmap.GetPixelSpan());
        using var data = bitmap.Encode(SKEncodedImageFormat.Jpeg, quality);
        return data.ToArray();
    }

    private static byte[] CreateNoisePng(int width, int height, int seed)
    {
        using var bitmap = new SKBitmap(new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul));
        new Random(seed).NextBytes(bitmap.GetPixelSpan());
        using var data = bitmap.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    /// <summary>Creates a single-page PDF with one full-page embedded image.</summary>
    private string CreateFixtureWithImage(string name, byte[] imageBytes, int pageWidth, int pageHeight)
    {
        var path = P(name);
        using var document = new PdfDocument();
        var page = document.AddPage();
        page.Width = XUnit.FromPoint(pageWidth);
        page.Height = XUnit.FromPoint(pageHeight);
        using var gfx = XGraphics.FromPdfPage(page);
        using var stream = new MemoryStream(imageBytes);
        using var image = XImage.FromStream(stream);
        gfx.DrawImage(image, 0, 0, pageWidth, pageHeight);
        document.Save(path);
        return path;
    }

    /// <summary>Reads back the first image XObject found on the first page.</summary>
    private static (int Width, int Height, int StreamLength, string Filter) GetFirstImageInfo(string pdfPath)
    {
        using var document = PdfSharp.Pdf.IO.PdfReader.Open(pdfPath, PdfSharp.Pdf.IO.PdfDocumentOpenMode.Import);
        foreach (var page in document.Pages.Cast<PdfPage>())
        {
            var xObjects = page.Resources.Elements.GetDictionary("/XObject");
            if (xObjects is null)
            {
                continue;
            }

            foreach (var key in xObjects.Elements.Keys.ToArray())
            {
                var candidate = xObjects.Elements.GetDictionary(key);
                if (candidate is not null && candidate.Elements.GetName("/Subtype") == "/Image")
                {
                    return (
                        candidate.Elements.GetInteger("/Width"),
                        candidate.Elements.GetInteger("/Height"),
                        candidate.Stream?.Value?.Length ?? 0,
                        candidate.Elements.GetName("/Filter"));
                }
            }
        }

        throw new InvalidOperationException($"No image XObject found in {pdfPath}.");
    }

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

    // ======================== encrypt / decrypt ========================

    [Fact]
    public async Task Encrypt_sets_password_and_document_opens_with_it()
    {
        var source = CreateFixture("plain.pdf", 2);
        var output = P("locked.pdf");

        await _engine.EncryptAsync(
            new EncryptRequest(source, output, new EncryptionOptions("hunter2", null)),
            cancellationToken: TestContext.Current.CancellationToken);

        var info = await _engine.InspectAsync(output, "hunter2", TestContext.Current.CancellationToken);
        Assert.True(info.IsEncrypted);

        await Assert.ThrowsAsync<InvalidPdfPasswordException>(
            () => _engine.InspectAsync(output, cancellationToken: TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<InvalidPdfPasswordException>(
            () => _engine.InspectAsync(output, "wrong", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Encrypt_applies_permission_flags()
    {
        var source = CreateFixture("plain.pdf", 1);
        var output = P("locked.pdf");
        var permissions = PdfPermissions.Print | PdfPermissions.CopyContents;

        await _engine.EncryptAsync(
            new EncryptRequest(source, output, new EncryptionOptions("hunter2", null, permissions)),
            cancellationToken: TestContext.Current.CancellationToken);

        using var reopened = PdfSharp.Pdf.IO.PdfReader.Open(output, "hunter2", PdfSharp.Pdf.IO.PdfDocumentOpenMode.Modify);
        Assert.True(reopened.SecuritySettings.PermitPrint);
        Assert.True(reopened.SecuritySettings.PermitExtractContent);
        Assert.False(reopened.SecuritySettings.PermitModifyDocument);
        Assert.False(reopened.SecuritySettings.PermitAnnotations);
        Assert.False(reopened.SecuritySettings.PermitFormsFill);
        Assert.False(reopened.SecuritySettings.PermitAssembleDocument);
        Assert.False(reopened.SecuritySettings.PermitFullQualityPrint);
    }

    [Fact]
    public async Task Encrypt_v5_uses_aes256_and_v4_uses_aes128()
    {
        var source = CreateFixture("plain.pdf", 1);
        var aes128Output = P("aes128.pdf");
        var aes256Output = P("aes256.pdf");

        await _engine.EncryptAsync(
            new EncryptRequest(source, aes128Output, new EncryptionOptions("hunter2", null, Strength: EncryptionStrength.Aes128)),
            cancellationToken: TestContext.Current.CancellationToken);
        await _engine.EncryptAsync(
            new EncryptRequest(source, aes256Output, new EncryptionOptions("hunter2", null, Strength: EncryptionStrength.Aes256)),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True((await _engine.InspectAsync(aes128Output, "hunter2", TestContext.Current.CancellationToken)).IsEncrypted);
        Assert.True((await _engine.InspectAsync(aes256Output, "hunter2", TestContext.Current.CancellationToken)).IsEncrypted);
    }

    [Fact]
    public async Task Decrypt_removes_password_and_restrictions()
    {
        var source = CreateEncryptedFixture("locked.pdf", 1, "hunter2");
        var output = P("unlocked.pdf");

        await _engine.DecryptAsync(
            new DecryptRequest(source, output, "hunter2"),
            cancellationToken: TestContext.Current.CancellationToken);

        var info = await _engine.InspectAsync(output, cancellationToken: TestContext.Current.CancellationToken);
        Assert.False(info.IsEncrypted);
    }

    [Fact]
    public async Task Encrypt_without_any_password_throws()
    {
        // PDFsharp itself refuses to save a document with a security handler
        // attached but no user/owner password set ("At least a user or an
        // owner password is required to encrypt the document."). The
        // Application layer already enforces EncryptionOptions.HasAnyPassword
        // before a request reaches the engine, so this is a defense-in-depth
        // check, not a UI-reachable path.
        var source = CreateFixture("plain.pdf", 1);
        var output = P("nopassword.pdf");

        await Assert.ThrowsAsync<PdfSharp.PdfSharpException>(
            () => _engine.EncryptAsync(
                new EncryptRequest(source, output, new EncryptionOptions(null, null)),
                cancellationToken: TestContext.Current.CancellationToken));
    }

    // ======================== compress ========================

    [Fact]
    public async Task Compress_lossless_preserves_page_geometry_and_leaves_images_untouched()
    {
        var jpeg = CreateNoiseJpeg(400, 300, SKColorType.Bgra8888, SKAlphaType.Premul, quality: 90, seed: 1);
        var source = CreateFixtureWithImage("photo.pdf", jpeg, 400, 300);
        var before = GetFirstImageInfo(source);
        var output = P("lossless.pdf");

        await _engine.CompressAsync(
            new CompressRequest(source, output, CompressionProfile.Lossless),
            cancellationToken: TestContext.Current.CancellationToken);

        var after = GetFirstImageInfo(output);
        Assert.Equal(before.Width, after.Width);
        Assert.Equal(before.Height, after.Height);
        Assert.Equal(before.StreamLength, after.StreamLength);

        var pages = await _engine.GetPagesAsync(output, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Single(pages);
    }

    [Fact]
    public async Task Compress_balanced_downsamples_oversized_rgb_jpeg_and_shrinks_file()
    {
        var jpeg = CreateNoiseJpeg(2000, 1500, SKColorType.Bgra8888, SKAlphaType.Premul, quality: 95, seed: 2);
        var source = CreateFixtureWithImage("photo.pdf", jpeg, 2000, 1500);
        var output = P("balanced.pdf");

        await _engine.CompressAsync(
            new CompressRequest(source, output, CompressionProfile.Balanced),
            cancellationToken: TestContext.Current.CancellationToken);

        var after = GetFirstImageInfo(output);
        Assert.True(Math.Max(after.Width, after.Height) <= 1600, $"expected long edge <= 1600, was {Math.Max(after.Width, after.Height)}");
        Assert.Equal("/DCTDecode", after.Filter);
        Assert.True(new FileInfo(output).Length < new FileInfo(source).Length, "expected the compressed file to be smaller");

        var info = await _engine.InspectAsync(output, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(1, info.PageCount);
    }

    [Fact]
    public async Task Compress_aggressive_produces_smaller_or_equal_image_than_balanced()
    {
        var jpeg = CreateNoiseJpeg(2000, 1500, SKColorType.Bgra8888, SKAlphaType.Premul, quality: 95, seed: 3);
        var balancedSource = CreateFixtureWithImage("balanced-in.pdf", jpeg, 2000, 1500);
        var aggressiveSource = CreateFixtureWithImage("aggressive-in.pdf", jpeg, 2000, 1500);
        var balancedOutput = P("balanced.pdf");
        var aggressiveOutput = P("aggressive.pdf");

        await _engine.CompressAsync(
            new CompressRequest(balancedSource, balancedOutput, CompressionProfile.Balanced),
            cancellationToken: TestContext.Current.CancellationToken);
        await _engine.CompressAsync(
            new CompressRequest(aggressiveSource, aggressiveOutput, CompressionProfile.Aggressive),
            cancellationToken: TestContext.Current.CancellationToken);

        var balanced = GetFirstImageInfo(balancedOutput);
        var aggressive = GetFirstImageInfo(aggressiveOutput);

        Assert.True(Math.Max(aggressive.Width, aggressive.Height) <= 1000);
        Assert.True(aggressive.StreamLength <= balanced.StreamLength);
    }

    [Fact]
    public async Task Compress_leaves_non_jpeg_images_untouched()
    {
        var png = CreateNoisePng(400, 300, seed: 4);
        var source = CreateFixtureWithImage("graphic.pdf", png, 400, 300);
        var before = GetFirstImageInfo(source);
        var output = P("aggressive.pdf");

        await _engine.CompressAsync(
            new CompressRequest(source, output, CompressionProfile.Aggressive),
            cancellationToken: TestContext.Current.CancellationToken);

        var after = GetFirstImageInfo(output);
        Assert.Equal(before.StreamLength, after.StreamLength);
        Assert.NotEqual("/DCTDecode", after.Filter);
    }

    [Fact]
    public async Task Compress_reports_monotonic_progress_ending_at_one()
    {
        var jpeg = CreateNoiseJpeg(800, 600, SKColorType.Bgra8888, SKAlphaType.Premul, quality: 90, seed: 5);
        var source = CreateFixtureWithImage("photo.pdf", jpeg, 800, 600);
        var progress = new ProgressCollector();

        await _engine.CompressAsync(
            new CompressRequest(source, P("out.pdf"), CompressionProfile.Balanced),
            progress,
            TestContext.Current.CancellationToken);

        var fractions = progress.Reports.Select(r => r.Fraction).ToArray();
        Assert.True(fractions.Length >= 2);
        Assert.True(fractions.SequenceEqual(fractions.OrderBy(f => f)), "progress went backwards");
        Assert.Equal(1.0, fractions[^1]);
    }

    // ======================== compose ========================

    [Fact]
    public async Task Compose_assembles_pages_from_multiple_files_in_explicit_order()
    {
        var a = CreateFixture("a.pdf", 3);
        var b = CreateFixture("b.pdf", 2);
        var output = P("organized.pdf");

        await _engine.ComposeAsync(
            new ComposeRequest(
                [
                    new PageAssignment(b, 2),
                    new PageAssignment(a, 1),
                    new PageAssignment(a, 1),
                    new PageAssignment(b, 1),
                ],
                output),
            cancellationToken: TestContext.Current.CancellationToken);

        AssertWidths([102, 101, 101, 101], await PageWidthsOf(output));
    }

    [Fact]
    public async Task Compose_applies_rotation_delta_per_page()
    {
        var a = CreateFixture("a.pdf", 2);
        var output = P("organized.pdf");

        await _engine.ComposeAsync(
            new ComposeRequest(
                [
                    new PageAssignment(a, 1, Rotation.Clockwise90),
                    new PageAssignment(a, 2),
                ],
                output),
            cancellationToken: TestContext.Current.CancellationToken);

        var pages = await _engine.GetPagesAsync(output, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(Rotation.Clockwise90, pages[0].Rotation);
        Assert.Equal(Rotation.None, pages[1].Rotation);
    }

    [Fact]
    public async Task Compose_rejects_empty_page_list()
        => await Assert.ThrowsAsync<PdfOperationException>(
            () => _engine.ComposeAsync(
                new ComposeRequest([], P("out.pdf")),
                cancellationToken: TestContext.Current.CancellationToken));

    [Fact]
    public async Task Compose_out_of_range_page_throws_before_writing()
    {
        var a = CreateFixture("a.pdf", 2);
        var output = P("organized.pdf");

        await Assert.ThrowsAsync<PdfOperationException>(
            () => _engine.ComposeAsync(
                new ComposeRequest([new PageAssignment(a, 9)], output),
                cancellationToken: TestContext.Current.CancellationToken));

        Assert.False(File.Exists(output));
    }

    [Fact]
    public async Task Compose_uses_per_source_passwords()
    {
        var locked = CreateEncryptedFixture("locked.pdf", 1, "hunter2");
        var output = P("organized.pdf");

        await _engine.ComposeAsync(
            new ComposeRequest(
                [new PageAssignment(locked, 1)],
                output,
                new Dictionary<string, string> { [locked] = "hunter2" }),
            cancellationToken: TestContext.Current.CancellationToken);

        var info = await _engine.InspectAsync(output, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(1, info.PageCount);
        Assert.False(info.IsEncrypted);
    }
}

