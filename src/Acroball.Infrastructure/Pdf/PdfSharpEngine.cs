using Microsoft.Extensions.Logging;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using SkiaSharp;
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
/// Compose throws <see cref="NotSupportedException"/> until the visual
/// organizer milestone lands. Encrypt/Decrypt use PDFsharp's built-in
/// AES-128/256 security handler (<see cref="PdfDocument.SecurityHandler"/>);
/// note that <see cref="PdfPermissions.ExtractForAccessibility"/> has no
/// equivalent in PDFsharp 6.2.4 and is silently unmappable on encrypt.
/// </para>
/// <para>
/// Compress always rebuilds the document page-by-page (dropping objects no
/// longer reachable from any page) and, for <see cref="CompressionProfile.Balanced"/>
/// and <see cref="CompressionProfile.Aggressive"/>, additionally recompresses
/// embedded images via SkiaSharp. See ADR-0009 for exactly which images
/// qualify (JPEG, DeviceRGB/DeviceGray, no transparency mask) and why the
/// rest are left untouched.
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
        => Task.Run(
            () =>
            {
                using var document = Open(request.InputFile, request.CurrentPassword, PdfDocumentOpenMode.Modify);

                document.SecurityHandler.UserPassword = request.Options.UserPassword ?? string.Empty;
                document.SecurityHandler.OwnerPassword = request.Options.OwnerPassword ?? string.Empty;

                if (request.Options.Strength == EncryptionStrength.Aes256)
                {
                    document.SecurityHandler.SetEncryptionToV5(true);
                }
                else
                {
                    document.SecurityHandler.SetEncryptionToV4UsingAES(true);
                }

                var permissions = request.Options.Permissions;
                document.SecuritySettings.PermitPrint = permissions.HasFlag(PdfPermissions.Print);
                document.SecuritySettings.PermitModifyDocument = permissions.HasFlag(PdfPermissions.ModifyContents);
                document.SecuritySettings.PermitExtractContent = permissions.HasFlag(PdfPermissions.CopyContents);
                document.SecuritySettings.PermitAnnotations = permissions.HasFlag(PdfPermissions.Annotate);
                document.SecuritySettings.PermitFormsFill = permissions.HasFlag(PdfPermissions.FillForms);
                document.SecuritySettings.PermitAssembleDocument = permissions.HasFlag(PdfPermissions.AssembleDocument);
                document.SecuritySettings.PermitFullQualityPrint = permissions.HasFlag(PdfPermissions.PrintHighQuality);
                // PdfPermissions.ExtractForAccessibility has no PDFsharp 6.2.4 equivalent
                // (public or internal) — silently unmappable, not settable here.

                SaveAtomic(document, request.OutputFile, cancellationToken);
                progress?.Report(new OperationProgress(1.0, "Done"));
                _logger.LogInformation("Encrypted {Input} into {Output}", request.InputFile, request.OutputFile);
            },
            cancellationToken);

    /// <inheritdoc />
    public Task DecryptAsync(
        DecryptRequest request,
        IProgress<OperationProgress>? progress = null,
        CancellationToken cancellationToken = default)
        => Task.Run(
            () =>
            {
                using var document = Open(request.InputFile, request.Password, PdfDocumentOpenMode.Modify);
                document.SecurityHandler.SetEncryptionToNoneAndResetPasswords();

                SaveAtomic(document, request.OutputFile, cancellationToken);
                progress?.Report(new OperationProgress(1.0, "Done"));
                _logger.LogInformation("Decrypted {Input} into {Output}", request.InputFile, request.OutputFile);
            },
            cancellationToken);

    /// <inheritdoc />
    public Task CompressAsync(
        CompressRequest request,
        IProgress<OperationProgress>? progress = null,
        CancellationToken cancellationToken = default)
        => Task.Run(
            () =>
            {
                using var source = Open(request.InputFile, request.Password, PdfDocumentOpenMode.Import);
                using var output = new PdfDocument();

                var rebuildFraction = request.Profile == CompressionProfile.Lossless ? 1.0 : 0.5;
                for (var i = 0; i < source.PageCount; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    output.AddPage(source.Pages[i]);
                    progress?.Report(new OperationProgress(
                        rebuildFraction * (i + 1) / source.PageCount,
                        $"Rebuilding page {i + 1}/{source.PageCount}"));
                }

                var imagesRecompressed = 0;
                if (request.Profile != CompressionProfile.Lossless)
                {
                    imagesRecompressed = RecompressImages(output, request.Profile, progress, cancellationToken);
                }

                SaveAtomic(output, request.OutputFile, cancellationToken);
                progress?.Report(new OperationProgress(1.0, "Done"));
                _logger.LogInformation(
                    "Compressed {Input} into {Output} ({Profile} profile, {ImagesRecompressed} image(s) recompressed)",
                    request.InputFile, request.OutputFile, request.Profile, imagesRecompressed);
            },
            cancellationToken);

    /// <summary>
    /// Recompresses qualifying images across every page (and one level of
    /// nested Form XObjects) of <paramref name="document"/> in place. See
    /// ADR-0009 for the qualification criteria. Returns how many images were
    /// actually replaced with a smaller encoding.
    /// </summary>
    private static int RecompressImages(
        PdfDocument document,
        CompressionProfile profile,
        IProgress<OperationProgress>? progress,
        CancellationToken cancellationToken)
    {
        var (maxDimension, jpegQuality) = profile switch
        {
            CompressionProfile.Aggressive => (1000, 45),
            _ => (1600, 75), // Balanced
        };

        var seen = new HashSet<PdfDictionary>();
        var images = new List<PdfDictionary>();
        foreach (var page in document.Pages.Cast<PdfPage>())
        {
            CollectImageXObjects(page.Resources, seen, images, depth: 0);
        }

        var recompressedCount = 0;
        for (var i = 0; i < images.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (TryRecompressImage(images[i], maxDimension, jpegQuality))
            {
                recompressedCount++;
            }

            progress?.Report(new OperationProgress(
                0.5 + 0.5 * (i + 1) / images.Count,
                $"Recompressing images ({i + 1}/{images.Count})"));
        }

        return recompressedCount;
    }

    /// <summary>
    /// Walks one resources dictionary's <c>/XObject</c> entries, collecting
    /// image dictionaries and recursing one level into Form XObjects.
    /// <paramref name="seen"/> both de-duplicates images shared across pages
    /// and guards against reference cycles.
    /// </summary>
    private static void CollectImageXObjects(PdfDictionary resources, HashSet<PdfDictionary> seen, List<PdfDictionary> result, int depth)
    {
        if (depth > 4)
        {
            return;
        }

        var xObjects = resources.Elements.GetDictionary("/XObject");
        if (xObjects is null)
        {
            return;
        }

        foreach (var key in xObjects.Elements.Keys.ToArray())
        {
            var xObject = xObjects.Elements.GetDictionary(key);
            if (xObject is null || !seen.Add(xObject))
            {
                continue;
            }

            var subtype = xObject.Elements.GetName("/Subtype");
            if (subtype == "/Image")
            {
                result.Add(xObject);
            }
            else if (subtype == "/Form")
            {
                var nestedResources = xObject.Elements.GetDictionary("/Resources");
                if (nestedResources is not null)
                {
                    CollectImageXObjects(nestedResources, seen, result, depth + 1);
                }
            }
        }
    }

    /// <summary>
    /// Recompresses one image XObject in place if it qualifies (see
    /// ADR-0009), keeping the change only when the result is strictly
    /// smaller. Returns whether the image was replaced.
    /// </summary>
    private static bool TryRecompressImage(PdfDictionary imageDict, int maxDimension, int jpegQuality)
    {
        if (imageDict.Elements.GetBoolean("/ImageMask")
            || imageDict.Elements.ContainsKey("/SMask")
            || imageDict.Elements.ContainsKey("/Mask"))
        {
            return false;
        }

        if (imageDict.Elements.GetName("/Filter") != "/DCTDecode")
        {
            return false;
        }

        var colorSpace = imageDict.Elements.GetName("/ColorSpace");
        if (colorSpace != "/DeviceRGB" && colorSpace != "/DeviceGray")
        {
            return false;
        }

        var originalBytes = imageDict.Stream?.Value;
        if (originalBytes is null || originalBytes.Length == 0)
        {
            return false;
        }

        try
        {
            using var bitmap = SKBitmap.Decode(originalBytes);
            if (bitmap is null)
            {
                return false;
            }

            var longEdge = Math.Max(bitmap.Width, bitmap.Height);
            var targetWidth = bitmap.Width;
            var targetHeight = bitmap.Height;
            if (longEdge > maxDimension)
            {
                var scale = (double)maxDimension / longEdge;
                targetWidth = Math.Max(1, (int)Math.Round(bitmap.Width * scale));
                targetHeight = Math.Max(1, (int)Math.Round(bitmap.Height * scale));
            }

            // Bgra8888 is the color type PDFsharp/Skia round-trip reliably for
            // JPEG encode; Rgb888x fails to encode in this SkiaSharp build.
            var targetColorType = colorSpace == "/DeviceGray" ? SKColorType.Gray8 : SKColorType.Bgra8888;
            var targetAlpha = targetColorType == SKColorType.Gray8 ? SKAlphaType.Opaque : SKAlphaType.Premul;

            var unchangedSize = targetWidth == bitmap.Width && targetHeight == bitmap.Height && bitmap.ColorType == targetColorType;
            using var working = unchangedSize
                ? bitmap.Copy(targetColorType)
                : bitmap.Resize(new SKImageInfo(targetWidth, targetHeight, targetColorType, targetAlpha), new SKSamplingOptions(SKCubicResampler.CatmullRom));

            if (working is null)
            {
                return false;
            }

            using var encodedData = working.Encode(SKEncodedImageFormat.Jpeg, jpegQuality);
            if (encodedData is null)
            {
                return false;
            }

            var newBytes = encodedData.ToArray();
            if (newBytes.Length >= originalBytes.Length)
            {
                return false;
            }

            imageDict.Stream!.Value = newBytes;
            imageDict.Elements.SetInteger("/Length", newBytes.Length);
            imageDict.Elements.SetInteger("/Width", working.Width);
            imageDict.Elements.SetInteger("/Height", working.Height);
            imageDict.Elements.SetInteger("/BitsPerComponent", 8);
            imageDict.Elements.Remove("/DecodeParms");
            imageDict.Elements.Remove("/Decode");
            return true;
        }
        catch (Exception)
        {
            // A single malformed or unsupported image must not fail the
            // whole compress job; leave it exactly as it was.
            return false;
        }
    }

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

