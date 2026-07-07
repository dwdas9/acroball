using System.Runtime.Versioning;
using Microsoft.Extensions.Logging.Abstractions;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using Acroball.Domain.Exceptions;
using Acroball.Infrastructure.Pdf;
using Xunit;

namespace Acroball.Infrastructure.Tests;

/// <summary>
/// Integration tests over the PDFtoImage/PDFium-backed render service.
/// </summary>
[SupportedOSPlatform("windows")]
[SupportedOSPlatform("macos")]
[SupportedOSPlatform("linux")]
public sealed class PdfRenderServiceTests : IDisposable
{
    private readonly string _dir;
    private readonly PdfRenderService _service = new(NullLogger<PdfRenderService>.Instance);

    public PdfRenderServiceTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "Acroball-render-tests-" + Guid.NewGuid().ToString("N"));
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

    private string P(string name) => Path.Combine(_dir, name);

    private string CreateFixture(string name, int pageWidth, int pageHeight, string? userPassword = null)
    {
        var path = P(name);
        using var document = new PdfDocument();
        var page = document.AddPage();
        page.Width = XUnit.FromPoint(pageWidth);
        page.Height = XUnit.FromPoint(pageHeight);
        using var gfx = XGraphics.FromPdfPage(page);
        gfx.DrawRectangle(XBrushes.Blue, 10, 10, pageWidth - 20, pageHeight - 20);

        if (userPassword is not null)
        {
            document.SecuritySettings.UserPassword = userPassword;
        }

        document.Save(path);
        return path;
    }

    [Fact]
    public async Task RenderPage_produces_png_matching_target_width_and_aspect_ratio()
    {
        var path = CreateFixture("a.pdf", 300, 400);

        var image = await _service.RenderPageAsync(path, pageNumber: 1, targetWidthPx: 150, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(1, image.PageNumber);
        Assert.Equal(150, image.WidthPx);
        Assert.Equal(200, image.HeightPx); // 300x400 scaled to width 150 keeps a 4:3 ratio
        Assert.True(image.EncodedPng.Length > 0);
        Assert.Equal(0x89, image.EncodedPng[0]); // PNG signature byte
    }

    [Fact]
    public async Task RenderPage_with_correct_password_succeeds()
    {
        var path = CreateFixture("locked.pdf", 300, 400, userPassword: "hunter2");

        var image = await _service.RenderPageAsync(path, pageNumber: 1, targetWidthPx: 100, password: "hunter2", cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(image.EncodedPng.Length > 0);
    }

    [Fact]
    public async Task RenderPage_with_wrong_password_throws_invalid_password()
    {
        var path = CreateFixture("locked.pdf", 300, 400, userPassword: "hunter2");

        var ex = await Assert.ThrowsAsync<InvalidPdfPasswordException>(
            () => _service.RenderPageAsync(path, pageNumber: 1, targetWidthPx: 100, password: "wrong", cancellationToken: TestContext.Current.CancellationToken));

        Assert.Equal(path, ex.FilePath);
    }

    [Fact]
    public async Task RenderPage_missing_file_throws_file_not_found()
        => await Assert.ThrowsAsync<FileNotFoundException>(
            () => _service.RenderPageAsync(P("nope.pdf"), pageNumber: 1, targetWidthPx: 100, cancellationToken: TestContext.Current.CancellationToken));

    [Fact]
    public async Task RenderPage_garbage_file_throws_corrupt()
    {
        var path = P("garbage.pdf");
        await File.WriteAllBytesAsync(path, "this is definitely not a pdf"u8.ToArray(), TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<CorruptPdfException>(
            () => _service.RenderPageAsync(path, pageNumber: 1, targetWidthPx: 100, cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Concurrent_render_calls_are_serialized_and_all_succeed()
    {
        var path = CreateFixture("a.pdf", 300, 400);

        var tasks = Enumerable.Range(0, 8)
            .Select(_ => _service.RenderPageAsync(path, pageNumber: 1, targetWidthPx: 80, cancellationToken: TestContext.Current.CancellationToken))
            .ToArray();

        var results = await Task.WhenAll(tasks);

        Assert.All(results, r => Assert.True(r.EncodedPng.Length > 0));
    }
}
