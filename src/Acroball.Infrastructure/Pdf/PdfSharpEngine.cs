using Microsoft.Extensions.Logging;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using Acroball.Application.Abstractions;
using Acroball.Application.Operations;
using Acroball.Domain;
using Acroball.Domain.Exceptions;

namespace Acroball.Infrastructure.Pdf;

/// <summary>
/// <see cref="IPdfEngine"/> implemented over PDFsharp (ADR-0002).
/// </summary>
/// <remarks>
/// <para>
/// PDFsharp's API is synchronous; every operation here runs on the thread
/// pool via <see cref="Task.Run(Action, CancellationToken)"/>, checks the
/// cancellation token between pages, and reports fractional progress.
/// The engine is stateless and registered as a singleton.
/// </para>
/// <para>
/// Outputs are written atomically (a sibling <c>*.tmp</c> file moved into
/// place), so a cancelled or failed operation never leaves a truncated PDF
/// at the destination. Multi-file operations (Split) may leave earlier,
/// fully written outputs behind on cancellation; that is deliberate.
/// </para>
/// <para>
/// Metadata semantics: non-null fields in
/// <see cref="UpdateMetadataRequest.Metadata"/> overwrite, null fields leave
/// the existing value untouched. <see cref="DocumentMetadata.Producer"/> is
/// read-only (PDFsharp stamps it) and is ignored on write.
/// </para>
/// <para>
/// Compose, Encrypt, Decrypt and Compress throw
/// <see cref="NotSupportedException"/> until their milestones (M3/M4) land.
/// </para>
/// </remarks>
public sealed class PdfSharpEngine : IPdfEngine
{
    private readonly ILogger<PdfSharpEngine> _logger;

    /// <summary>Creates the engine.</summary>
    public PdfSharpEngine(ILogger<PdfSharpEngine> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<PdfDocumentInfo> InspectAsync(
        string filePath,
        string? password = null,
        CancellationToken cancellationToken = default)
        => Task.Run(
            () =>
            {
                var fileSize = new FileInfo(filePath).Exists ? new FileInfo(filePath).Length : 0L;
                var (document, wasEncrypted) = OpenDetectingEncryption(filePath, password);
                using (document)
                {
                    return new PdfDocumentInfo(
                        filePath,
                        document.PageCount,
                        fileSize,
                        wasEncrypted,
                        ReadMetadata(document),
                        FormatVersion(document.Version));
                }
            },
            cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<PdfPageInfo>> GetPagesAsync(
        string filePath,
        string? password = null,
        CancellationToken cancellationToken = default)
        => Task.Run<IReadOnlyList<PdfPageInfo>>(
            () =>
            {
                using var document = Open(filePath, password, PdfDocumentOpenMode.Import);
                var pages = new List<PdfPageInfo>(document.PageCount);

                for (var i = 0; i < document.PageCount; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var page = document.Pages[i];
                    pages.Add(new PdfPageInfo(
                        i + 1,
                        page.Width.Point,
                        page.Height.Point,
                        NormalizeRotation(page.Rotate)));
                }

                return pages;
            },
            cancellationToken);

    /// <inheritdoc />
    public Task MergeAsync(
        MergeRequest request,
        IProgress<OperationProgress>? progress = null,
        CancellationToken cancellationToken = default)
        => Task.Run(
            () =>
            {
                if (request.InputFiles.Count == 0)
                {
                    throw new PdfOperationException("Select at least one file to merge.");
                }

                using var output = new PdfDocument();

                // First pass: open everything up front so a bad third file
                // fails before any work, not halfway through.
                var sources = new List<(string Path, PdfDocument Document)>(request.InputFiles.Count);
                try
                {
                    foreach (var inputPath in request.InputFiles)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        sources.Add((inputPath, Open(inputPath, PasswordFor(request.Passwords, inputPath), PdfDocumentOpenMode.Import)));
                    }

                    var totalPages = sources.Sum(s => s.Document.PageCount);
                    var pagesDone = 0;

                    foreach (var (path, source) in sources)
                    {
                        var fileName = Path.GetFileName(path);
                        for (var i = 0; i < source.PageCount; i++)
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            output.AddPage(source.Pages[i]);
                            pagesDone++;
                            progress?.Report(new OperationProgress(
                                (double)pagesDone / totalPages,
                                $"Merging {fileName} ({pagesDone}/{totalPages} pages)"));
                        }
                    }
                }
                finally
                {
                    foreach (var (_, document) in sources)
                    {
                        document.Dispose();
                    }
                }

                SaveAtomic(output, request.OutputFile, cancellationToken);
                progress?.Report(new OperationProgress(1.0, "Done"));
                _logger.LogInformation("Merged {Count} files into {Output}", request.InputFiles.Count, request.OutputFile);
            },
            cancellationToken);

    /// <inheritdoc />
    public Task SplitAsync(
        SplitRequest request,
        IProgress<OperationProgress>? progress = null,
        CancellationToken cancellationToken = default)
        => Task.Run(
            () =>
            {
                using var source = Open(request.InputFile, request.Password, PdfDocumentOpenMode.Import);
                ValidateRanges(request.Ranges, source.PageCount);

                Directory.CreateDirectory(request.OutputDirectory);
                var sourceName = Path.GetFileNameWithoutExtension(request.InputFile);

                for (var rangeIndex = 0; rangeIndex < request.Ranges.Count; rangeIndex++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var range = request.Ranges[rangeIndex];
                    var fileName = OutputNameTemplate.Expand(
                        request.FileNameTemplate, sourceName, rangeIndex + 1, range.ToString());
                    var outputPath = Path.Combine(request.OutputDirectory, fileName);

                    using var part = new PdfDocument();
                    foreach (var pageNumber in range.Enumerate())
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        part.AddPage(source.Pages[pageNumber - 1]);
                    }

                    SaveAtomic(part, outputPath, cancellationToken);
                    progress?.Report(new OperationProgress(
                        (double)(rangeIndex + 1) / request.Ranges.Count,
                        $"Wrote {fileName} ({rangeIndex + 1}/{request.Ranges.Count})"));
                }

                _logger.LogInformation(
                    "Split {Input} into {Count} files in {Directory}",
                    request.InputFile, request.Ranges.Count, request.OutputDirectory);
            },
            cancellationToken);

    /// <inheritdoc />
    public Task ExtractPagesAsync(
        ExtractPagesRequest request,
        IProgress<OperationProgress>? progress = null,
        CancellationToken cancellationToken = default)
        => Task.Run(
            () =>
            {
                using var source = Open(request.InputFile, request.Password, PdfDocumentOpenMode.Import);
                ValidateRanges(request.Ranges, source.PageCount);

                using var output = new PdfDocument();
                var totalPages = request.Ranges.Sum(r => r.Count);
                var pagesDone = 0;

                foreach (var range in request.Ranges)
                {
                    foreach (var pageNumber in range.Enumerate())
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        output.AddPage(source.Pages[pageNumber - 1]);
                        pagesDone++;
                        progress?.Report(new OperationProgress(
                            (double)pagesDone / totalPages,
                            $"Extracting page {pageNumber} ({pagesDone}/{totalPages})"));
                    }
                }

                SaveAtomic(output, request.OutputFile, cancellationToken);
                progress?.Report(new OperationProgress(1.0, "Done"));
                _logger.LogInformation(
                    "Extracted {Pages} pages from {Input} to {Output}",
                    totalPages, request.InputFile, request.OutputFile);
            },
            cancellationToken);

    /// <inheritdoc />
    public Task RotatePagesAsync(
        RotatePagesRequest request,
        IProgress<OperationProgress>? progress = null,
        CancellationToken cancellationToken = default)
        => Task.Run(
            () =>
            {
                if (request.Rotation == Rotation.None)
                {
                    throw new PdfOperationException("Choose a rotation other than 0Â°.");
                }

                using var document = Open(request.InputFile, request.Password, PdfDocumentOpenMode.Modify);
                ValidateRanges(request.Ranges, document.PageCount);

                var pageNumbers = request.Ranges.SelectMany(r => r.Enumerate()).Distinct().ToList();
                var pagesDone = 0;

                foreach (var pageNumber in pageNumbers)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var page = document.Pages[pageNumber - 1];
                    page.Rotate = (int)NormalizeRotation(page.Rotate).Add(request.Rotation);
                    pagesDone++;
                    progress?.Report(new OperationProgress(
                        (double)pagesDone / pageNumbers.Count,
                        $"Rotating page {pageNumber} ({pagesDone}/{pageNumbers.Count})"));
                }

                SaveAtomic(document, request.OutputFile, cancellationToken);
                progress?.Report(new OperationProgress(1.0, "Done"));
                _logger.LogInformation(
                    "Rotated {Count} pages of {Input} by {Degrees}Â°",
                    pageNumbers.Count, request.InputFile, request.Rotation.ToDegrees());
            },
            cancellationToken);

    /// <inheritdoc />
    public Task UpdateMetadataAsync(
        UpdateMetadataRequest request,
        IProgress<OperationProgress>? progress = null,
        CancellationToken cancellationToken = default)
        => Task.Run(
            () =>
            {
                using var document = Open(request.InputFile, request.Password, PdfDocumentOpenMode.Modify);
                var metadata = request.Metadata;

                if (metadata.Title is not null)
                {
                    document.Info.Title = metadata.Title;
                }

                if (metadata.Author is not null)
                {
                    document.Info.Author = metadata.Author;
                }

                if (metadata.Subject is not null)
                {
                    document.Info.Subject = metadata.Subject;
                }

                if (metadata.Keywords is not null)
                {
                    document.Info.Keywords = metadata.Keywords;
                }

                if (metadata.Creator is not null)
                {
                    document.Info.Creator = metadata.Creator;
                }

                if (metadata.CreationDate is not null)
                {
                    document.Info.CreationDate = metadata.CreationDate.Value.LocalDateTime;
                }

                // Producer is stamped by PDFsharp and deliberately not written.
                // ModificationDate is set by PDFsharp on save.

                SaveAtomic(document, request.OutputFile, cancellationToken);
                progress?.Report(new OperationProgress(1.0, "Done"));
                _logger.LogInformation("Updated metadata of {Input} into {Output}", request.InputFile, request.OutputFile);
            },
            cancellationToken);

    /// <inheritdoc />
    public Task ComposeAsync(
        ComposeRequest request,
        IProgress<OperationProgress>? progress = null,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Compose ships with the visual organizer in Milestone 3.");

    /// <inheritdoc />
    public Task EncryptAsync(
        EncryptRequest request,
        IProgress<OperationProgress>? progress = null,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Protect ships in Milestone 4.");

    /// <inheritdoc />
    public Task DecryptAsync(
        DecryptRequest request,
        IProgress<OperationProgress>? progress = null,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Protect ships in Milestone 4.");

    /// <inheritdoc />
    public Task CompressAsync(
        CompressRequest request,
        IProgress<OperationProgress>? progress = null,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Compress ships in Milestone 4.");

    // ======================== helpers ========================

    private PdfDocument Open(string filePath, string? password, PdfDocumentOpenMode mode)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("Input PDF was not found.", filePath);
        }

        // TestPdfFile returns 0 when the file has no PDF header at all.
        int headerVersion;
        try
        {
            headerVersion = PdfReader.TestPdfFile(filePath);
        }
        catch (Exception ex)
        {
            throw new CorruptPdfException(filePath, ex);
        }

        if (headerVersion == 0)
        {
            throw new CorruptPdfException(filePath, new InvalidDataException("The file has no PDF header."));
        }

        try
        {
            return password is null
                ? PdfReader.Open(filePath, mode)
                : PdfReader.Open(filePath, password, mode);
        }
        catch (PdfReaderException ex)
        {
            // PDFsharp raises PdfReaderException both for password problems
            // and for structural ones; having already verified the header,
            // a failure here on an encrypted file is a password problem.
            // The distinction is validated by the integration tests.
            _logger.LogDebug(ex, "PdfReaderException opening {Path}", filePath);
            throw new InvalidPdfPasswordException(filePath);
        }
        catch (Exception ex)
        {
            throw new CorruptPdfException(filePath, ex);
        }
    }

    private (PdfDocument Document, bool WasEncrypted) OpenDetectingEncryption(string filePath, string? password)
    {
        // Try without a password first, even when one is supplied: that is
        // the only reliable way to report IsEncrypted for the inspect view.
        try
        {
            return (Open(filePath, password: null, PdfDocumentOpenMode.Import), false);
        }
        catch (InvalidPdfPasswordException) when (password is not null)
        {
            return (Open(filePath, password, PdfDocumentOpenMode.Import), true);
        }
    }

    private static void SaveAtomic(PdfDocument document, string outputPath, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var fullPath = Path.GetFullPath(outputPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

        var tempPath = fullPath + ".tmp";
        try
        {
            document.Save(tempPath);
            File.Move(tempPath, fullPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                try
                {
                    File.Delete(tempPath);
                }
                catch (IOException)
                {
                    // Leftover temp files are harmless; never mask the real error.
                }
            }
        }
    }

    private static void ValidateRanges(IReadOnlyList<PageRange> ranges, int pageCount)
    {
        if (ranges.Count == 0)
        {
            throw new PdfOperationException("No pages were selected.");
        }

        foreach (var range in ranges)
        {
            if (range.End > pageCount)
            {
                throw new PdfOperationException(
                    $"Range {range} is beyond the last page ({pageCount}).");
            }
        }
    }

    private static DocumentMetadata ReadMetadata(PdfDocument document)
    {
        var info = document.Info;

        DateTimeOffset? creation = null;
        DateTimeOffset? modification = null;
        try
        {
            if (info.Elements.ContainsKey("/CreationDate"))
            {
                creation = info.CreationDate;
            }

            if (info.Elements.ContainsKey("/ModDate"))
            {
                modification = info.ModificationDate;
            }
        }
        catch (Exception)
        {
            // Malformed date strings are common in the wild; treat as absent.
        }

        return new DocumentMetadata(
            NullIfEmpty(info.Title),
            NullIfEmpty(info.Author),
            NullIfEmpty(info.Subject),
            NullIfEmpty(info.Keywords),
            NullIfEmpty(info.Creator),
            NullIfEmpty(info.Producer),
            creation,
            modification);
    }

    private static string? NullIfEmpty(string value) => string.IsNullOrEmpty(value) ? null : value;

    private static Rotation NormalizeRotation(int degrees)
        => (Rotation)(((degrees % 360) + 360) % 360);

    private static string FormatVersion(int version) => $"{version / 10}.{version % 10}";

    private static string? PasswordFor(IReadOnlyDictionary<string, string>? passwords, string path)
        => passwords is not null && passwords.TryGetValue(path, out var password) ? password : null;
}

