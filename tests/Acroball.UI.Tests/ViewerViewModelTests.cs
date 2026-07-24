using Acroball.Application.Abstractions;
using Acroball.Domain;
using Acroball.Domain.Exceptions;
using Acroball.UI.Services;
using Acroball.UI.ViewModels;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Acroball.UI.Tests;

public sealed class ViewerViewModelTests
{
    /// <summary>The smallest possible valid PNG (a single transparent pixel), used to exercise real bitmap decoding.</summary>
    private static readonly byte[] MinimalPng = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=");

    [Fact]
    public void Constructor_initializes_with_expected_state()
    {
        var viewModel = CreateViewModel();

        Assert.Empty(viewModel.Pages);
        Assert.False(viewModel.HasDocument);
        Assert.False(viewModel.IsLoadingDocument);
        Assert.False(viewModel.HasPendingPassword);
        Assert.Equal(string.Empty, viewModel.PageCountCaption);
    }

    [Fact]
    public async Task OpenFileAsync_populates_pages_from_engine()
    {
        using var fixture = new PdfFixture();
        var alpha = fixture.CreateFile("Alpha.pdf");

        var pdfEngine = new Mock<IPdfEngine>();
        pdfEngine.Setup(x => x.GetPagesAsync(alpha, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PdfPageInfo>
            {
                new(1, 300, 400, Rotation.None),
                new(2, 300, 400, Rotation.None),
                new(3, 300, 400, Rotation.None),
            });

        var viewModel = CreateViewModel(pdfEngine: pdfEngine.Object);

        await viewModel.OpenFileAsync(alpha);

        Assert.True(viewModel.HasDocument);
        Assert.Equal(alpha, viewModel.CurrentFile);
        Assert.Equal("Alpha.pdf", viewModel.CurrentFileName);
        Assert.Equal(3, viewModel.Pages.Count);
        Assert.Equal([1, 2, 3], viewModel.Pages.Select(p => p.PageNumber));
        Assert.Equal("3 page(s)", viewModel.PageCountCaption);
    }

    [Fact]
    public async Task OpenFileAsync_missing_file_sets_error()
    {
        var viewModel = CreateViewModel();

        await viewModel.OpenFileAsync(@"C:\nope\missing.pdf");

        Assert.False(viewModel.HasDocument);
        Assert.Equal("\"missing.pdf\" could not be found.", viewModel.OpenFileError);
    }

    [Fact]
    public async Task OpenFileAsync_on_encrypted_file_opens_password_prompt()
    {
        using var fixture = new PdfFixture();
        var locked = fixture.CreateFile("Locked.pdf");

        var pdfEngine = new Mock<IPdfEngine>();
        pdfEngine.Setup(x => x.GetPagesAsync(locked, null, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidPdfPasswordException(locked));

        var viewModel = CreateViewModel(pdfEngine: pdfEngine.Object);

        await viewModel.OpenFileAsync(locked);

        Assert.True(viewModel.HasPendingPassword);
        Assert.Equal("Locked.pdf", viewModel.PendingPasswordFileName);
        Assert.False(viewModel.HasDocument);
    }

    [Fact]
    public async Task SubmitPendingPassword_with_correct_password_opens_document()
    {
        using var fixture = new PdfFixture();
        var locked = fixture.CreateFile("Locked.pdf");

        var pdfEngine = new Mock<IPdfEngine>();
        pdfEngine.Setup(x => x.GetPagesAsync(locked, null, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidPdfPasswordException(locked));
        pdfEngine.Setup(x => x.GetPagesAsync(locked, "hunter2", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PdfPageInfo> { new(1, 300, 400, Rotation.None) });

        var viewModel = CreateViewModel(pdfEngine: pdfEngine.Object);
        await viewModel.OpenFileAsync(locked);

        viewModel.PendingPasswordInput = "hunter2";
        await viewModel.SubmitPendingPasswordAsync();

        Assert.False(viewModel.HasPendingPassword);
        Assert.True(viewModel.HasDocument);
        Assert.Single(viewModel.Pages);
    }

    [Fact]
    public async Task SubmitPendingPassword_with_wrong_password_keeps_prompt_open_with_error()
    {
        using var fixture = new PdfFixture();
        var locked = fixture.CreateFile("Locked.pdf");

        var pdfEngine = new Mock<IPdfEngine>();
        pdfEngine.Setup(x => x.GetPagesAsync(locked, It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidPdfPasswordException(locked));

        var viewModel = CreateViewModel(pdfEngine: pdfEngine.Object);
        await viewModel.OpenFileAsync(locked);

        viewModel.PendingPasswordInput = "wrong";
        await viewModel.SubmitPendingPasswordAsync();

        Assert.True(viewModel.HasPendingPassword);
        Assert.Equal("Incorrect password.", viewModel.PendingPasswordError);
        Assert.False(viewModel.HasDocument);
    }

    [Fact]
    public async Task CloseDocument_clears_pages_and_current_file()
    {
        using var fixture = new PdfFixture();
        var alpha = fixture.CreateFile("Alpha.pdf");

        var pdfEngine = new Mock<IPdfEngine>();
        pdfEngine.Setup(x => x.GetPagesAsync(alpha, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PdfPageInfo> { new(1, 300, 400, Rotation.None) });

        var viewModel = CreateViewModel(pdfEngine: pdfEngine.Object);
        await viewModel.OpenFileAsync(alpha);

        viewModel.CloseDocument();

        Assert.False(viewModel.HasDocument);
        Assert.Empty(viewModel.Pages);
        Assert.Equal(string.Empty, viewModel.PageCountCaption);
    }

    [Fact]
    public async Task LoadPageImageAsync_calls_render_service_with_display_width_and_clears_loading_flag()
    {
        // Bitmap decoding itself needs Avalonia's IPlatformRenderInterface, which
        // isn't registered in a plain xUnit host (no running Avalonia Application) —
        // the same reason OrganizeViewModelTests never exercises LoadThumbnailAsync's
        // decode step either. This test verifies the call contract and state
        // machine instead of the decoded bitmap.
        using var fixture = new PdfFixture();
        var alpha = fixture.CreateFile("Alpha.pdf");

        var pdfEngine = new Mock<IPdfEngine>();
        pdfEngine.Setup(x => x.GetPagesAsync(alpha, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PdfPageInfo> { new(1, 300, 400, Rotation.None) });

        var renderService = new Mock<IPdfRenderService>();
        renderService.Setup(x => x.RenderPageAsync(alpha, 1, ViewerPageViewModel.DisplayWidthPx, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RenderedPageImage(1, 1, 1, MinimalPng));

        var viewModel = CreateViewModel(pdfEngine: pdfEngine.Object, pdfRenderService: renderService.Object);
        await viewModel.OpenFileAsync(alpha);
        var page = viewModel.Pages[0];

        await viewModel.LoadPageImageAsync(page);

        renderService.Verify(
            x => x.RenderPageAsync(alpha, 1, ViewerPageViewModel.DisplayWidthPx, null, It.IsAny<CancellationToken>()),
            Times.Once);
        Assert.False(page.IsLoadingImage);
    }

    [Fact]
    public async Task LoadPageImageAsync_failure_sets_render_error()
    {
        using var fixture = new PdfFixture();
        var alpha = fixture.CreateFile("Alpha.pdf");

        var pdfEngine = new Mock<IPdfEngine>();
        pdfEngine.Setup(x => x.GetPagesAsync(alpha, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PdfPageInfo> { new(1, 300, 400, Rotation.None) });

        var renderService = new Mock<IPdfRenderService>();
        renderService.Setup(x => x.RenderPageAsync(alpha, 1, ViewerPageViewModel.DisplayWidthPx, null, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new CorruptPdfException(alpha, new InvalidDataException("bad")));

        var viewModel = CreateViewModel(pdfEngine: pdfEngine.Object, pdfRenderService: renderService.Object);
        await viewModel.OpenFileAsync(alpha);
        var page = viewModel.Pages[0];

        await viewModel.LoadPageImageAsync(page);

        Assert.Null(page.RenderedImage);
        Assert.False(page.IsLoadingImage);
        Assert.Equal("Preview unavailable", page.RenderError);
    }

    [Fact]
    public async Task UnloadPageImage_clears_cancellation_and_error_state()
    {
        using var fixture = new PdfFixture();
        var alpha = fixture.CreateFile("Alpha.pdf");

        var pdfEngine = new Mock<IPdfEngine>();
        pdfEngine.Setup(x => x.GetPagesAsync(alpha, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PdfPageInfo> { new(1, 300, 400, Rotation.None) });

        var renderService = new Mock<IPdfRenderService>();
        renderService.Setup(x => x.RenderPageAsync(alpha, 1, ViewerPageViewModel.DisplayWidthPx, null, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new CorruptPdfException(alpha, new InvalidDataException("bad")));

        var viewModel = CreateViewModel(pdfEngine: pdfEngine.Object, pdfRenderService: renderService.Object);
        await viewModel.OpenFileAsync(alpha);
        var page = viewModel.Pages[0];
        await viewModel.LoadPageImageAsync(page);
        Assert.NotNull(page.RenderError);

        viewModel.UnloadPageImage(page);

        Assert.Null(page.RenderedImage);
        Assert.Null(page.RenderCancellation);
        Assert.Null(page.RenderError);
        Assert.False(page.IsLoadingImage);
    }

    [Fact]
    public async Task OpenFileAsync_populates_outline_from_engine()
    {
        using var fixture = new PdfFixture();
        var alpha = fixture.CreateFile("Alpha.pdf");

        var pdfEngine = new Mock<IPdfEngine>();
        pdfEngine.Setup(x => x.GetPagesAsync(alpha, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PdfPageInfo> { new(1, 300, 400, Rotation.None), new(2, 300, 400, Rotation.None) });
        pdfEngine.Setup(x => x.GetOutlineAsync(alpha, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PdfOutlineNode>
            {
                new("Chapter 1", 1, true, [new PdfOutlineNode("Section 1.1", 2, false, [])]),
            });

        var viewModel = CreateViewModel(pdfEngine: pdfEngine.Object);

        await viewModel.OpenFileAsync(alpha);

        Assert.True(viewModel.HasOutline);
        Assert.Single(viewModel.Outline);
        Assert.Equal("Chapter 1", viewModel.Outline[0].Title);
        Assert.Equal(1, viewModel.Outline[0].DestinationPageNumber);
        Assert.Single(viewModel.Outline[0].Children);
        Assert.Equal("Section 1.1", viewModel.Outline[0].Children[0].Title);
    }

    [Fact]
    public async Task OpenFileAsync_when_outline_read_fails_still_opens_document()
    {
        using var fixture = new PdfFixture();
        var alpha = fixture.CreateFile("Alpha.pdf");

        var pdfEngine = new Mock<IPdfEngine>();
        pdfEngine.Setup(x => x.GetPagesAsync(alpha, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PdfPageInfo> { new(1, 300, 400, Rotation.None) });
        pdfEngine.Setup(x => x.GetOutlineAsync(alpha, null, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new PdfOperationException("Outline read failed."));

        var viewModel = CreateViewModel(pdfEngine: pdfEngine.Object);

        await viewModel.OpenFileAsync(alpha);

        Assert.True(viewModel.HasDocument);
        Assert.Single(viewModel.Pages);
        Assert.False(viewModel.HasOutline);
        Assert.Empty(viewModel.Outline);
    }

    [Fact]
    public void RequestScrollToPage_raises_scroll_to_page_requested()
    {
        var viewModel = CreateViewModel();
        var raised = new List<int>();
        viewModel.ScrollToPageRequested += raised.Add;

        viewModel.RequestScrollToPage(3);

        Assert.Equal([3], raised);
    }

    private static ViewerViewModel CreateViewModel(IPdfEngine? pdfEngine = null, IPdfRenderService? pdfRenderService = null)
    {
        pdfEngine ??= new Mock<IPdfEngine>().Object;

        if (pdfRenderService is null)
        {
            var renderMock = new Mock<IPdfRenderService>();
            renderMock.Setup(x => x.RenderPageAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("Rendering is not exercised by this test."));
            pdfRenderService = renderMock.Object;
        }

        return new ViewerViewModel(
            pdfEngine,
            pdfRenderService,
            new StubFileDialogService(),
            NullLogger<ViewerViewModel>.Instance);
    }

    private sealed class StubFileDialogService : IFileDialogService
    {
        public Task<IReadOnlyList<string>?> PickFilesAsync() => Task.FromResult<IReadOnlyList<string>?>(null);

        public Task<string?> PickSaveFileAsync(string initialFileName = "") => Task.FromResult<string?>(null);

        public Task<string?> PickFolderAsync() => Task.FromResult<string?>(null);
    }

    private sealed class PdfFixture : IDisposable
    {
        public PdfFixture()
        {
            DirectoryPath = Path.Combine(Path.GetTempPath(), $"acroball-ui-tests-{Guid.NewGuid():N}");
            Directory.CreateDirectory(DirectoryPath);
        }

        public string DirectoryPath { get; }

        public string CreateFile(string fileName)
        {
            var path = Path.Combine(DirectoryPath, fileName);
            File.WriteAllText(path, "%PDF-1.4\n%");
            return path;
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(DirectoryPath))
                {
                    Directory.Delete(DirectoryPath, recursive: true);
                }
            }
            catch
            {
                // Best-effort cleanup only.
            }
        }
    }
}
