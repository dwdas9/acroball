using System.Globalization;
using System.Text;
using Microsoft.Extensions.Logging;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using PdfSharp.Pdf.AcroForms;
using PdfSharp.Pdf.IO;
using SkiaSharp;
using Acroball.Application.Abstractions;
using Acroball.Application.Operations;
using Acroball.Domain;
using Acroball.Domain.Annotations;
using Acroball.Domain.Exceptions;
using Acroball.Domain.Forms;

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
/// Compose assembles an explicit page list (<see cref="PageAssignment"/>),
/// possibly spanning several source files, into one output document — the
/// primitive behind the visual organizer's reorder/delete/rotate/cross-file
/// moves. Encrypt/Decrypt use PDFsharp's built-in AES-128/256 security
/// handler (<see cref="PdfDocument.SecurityHandler"/>); note that
/// <see cref="PdfPermissions.ExtractForAccessibility"/> has no equivalent in
/// PDFsharp 6.2.4 and is silently unmappable on encrypt.
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
    public Task<IReadOnlyList<PdfOutlineNode>> GetOutlineAsync(
        string filePath,
        string? password = null,
        CancellationToken cancellationToken = default)
        => Task.Run<IReadOnlyList<PdfOutlineNode>>(
            () =>
            {
                using var document = Open(filePath, password, PdfDocumentOpenMode.Import);
                return BuildOutlineNodes(document, document.Outlines, depth: 0);
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
        => Task.Run(
            () =>
            {
                if (request.Pages.Count == 0)
                {
                    throw new PdfOperationException("No pages were selected.");
                }

                using var output = new PdfDocument();
                var openSources = new Dictionary<string, PdfDocument>();
                try
                {
                    var pagesDone = 0;
                    foreach (var assignment in request.Pages)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        if (!openSources.TryGetValue(assignment.SourceFile, out var source))
                        {
                            source = Open(assignment.SourceFile, PasswordFor(request.Passwords, assignment.SourceFile), PdfDocumentOpenMode.Import);
                            openSources[assignment.SourceFile] = source;
                        }

                        if (assignment.SourcePageNumber < 1 || assignment.SourcePageNumber > source.PageCount)
                        {
                            throw new PdfOperationException(
                                $"Page {assignment.SourcePageNumber} is beyond the last page of \"{Path.GetFileName(assignment.SourceFile)}\" ({source.PageCount}).");
                        }

                        var addedPage = output.AddPage(source.Pages[assignment.SourcePageNumber - 1]);
                        if (assignment.RotationDelta != Rotation.None)
                        {
                            addedPage.Rotate = (int)NormalizeRotation(addedPage.Rotate).Add(assignment.RotationDelta);
                        }

                        pagesDone++;
                        progress?.Report(new OperationProgress(
                            (double)pagesDone / request.Pages.Count,
                            $"Assembling page {pagesDone}/{request.Pages.Count}"));
                    }

                    SaveAtomic(output, request.OutputFile, cancellationToken);
                    progress?.Report(new OperationProgress(1.0, "Done"));
                    _logger.LogInformation(
                        "Composed {PageCount} page(s) from {SourceCount} file(s) into {Output}",
                        request.Pages.Count, openSources.Count, request.OutputFile);
                }
                finally
                {
                    foreach (var source in openSources.Values)
                    {
                        source.Dispose();
                    }
                }
            },
            cancellationToken);

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

    /// <inheritdoc />
    public Task SaveAnnotationsAsync(
        SaveAnnotationsRequest request,
        IProgress<OperationProgress>? progress = null,
        CancellationToken cancellationToken = default)
        => Task.Run(
            () =>
            {
                if (request.Annotations.Count == 0)
                {
                    throw new PdfOperationException("No annotations were specified.");
                }

                using var document = Open(request.InputFile, request.Password, PdfDocumentOpenMode.Modify);

                var annotationsDone = 0;
                foreach (var annotation in request.Annotations)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (annotation.PageNumber < 1 || annotation.PageNumber > document.PageCount)
                    {
                        throw new PdfOperationException(
                            $"Page {annotation.PageNumber} is beyond the last page of \"{Path.GetFileName(request.InputFile)}\" ({document.PageCount}).");
                    }

                    AddAnnotation(document, document.Pages[annotation.PageNumber - 1], annotation);

                    annotationsDone++;
                    progress?.Report(new OperationProgress(
                        (double)annotationsDone / request.Annotations.Count,
                        $"Adding annotation {annotationsDone}/{request.Annotations.Count}"));
                }

                SaveAtomic(document, request.OutputFile, cancellationToken);
                progress?.Report(new OperationProgress(1.0, "Done"));
                _logger.LogInformation(
                    "Added {Count} annotation(s) to {Input} into {Output}",
                    request.Annotations.Count, request.InputFile, request.OutputFile);
            },
            cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<PdfFormFieldInfo>> GetFormFieldsAsync(
        string filePath,
        string? password = null,
        CancellationToken cancellationToken = default)
        => Task.Run<IReadOnlyList<PdfFormFieldInfo>>(
            () =>
            {
                using var document = Open(filePath, password, PdfDocumentOpenMode.Import);
                if (!HasAcroForm(document))
                {
                    return [];
                }

                var form = document.AcroForm;
                var fields = new List<PdfFormFieldInfo>(form.Fields.Count);
                for (var i = 0; i < form.Fields.Count; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    fields.Add(ToFieldInfo(form.Fields[i]));
                }

                return fields;
            },
            cancellationToken);

    /// <inheritdoc />
    public Task FillFormAsync(
        FillFormRequest request,
        IProgress<OperationProgress>? progress = null,
        CancellationToken cancellationToken = default)
        => Task.Run(
            () =>
            {
                if (request.Values.Count == 0)
                {
                    throw new PdfOperationException("No field values were specified.");
                }

                using var document = Open(request.InputFile, request.Password, PdfDocumentOpenMode.Modify);
                if (!HasAcroForm(document))
                {
                    throw new PdfOperationException($"\"{Path.GetFileName(request.InputFile)}\" has no fillable form fields.");
                }

                var form = document.AcroForm;

                var valuesDone = 0;
                foreach (var value in request.Values)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var field = FindField(form, value.FullyQualifiedName)
                        ?? throw new PdfOperationException($"Field \"{value.FullyQualifiedName}\" was not found.");

                    SetFieldValue(field, value.Value);

                    valuesDone++;
                    progress?.Report(new OperationProgress(
                        (double)valuesDone / request.Values.Count,
                        $"Filling field {valuesDone}/{request.Values.Count}"));
                }

                if (request.FlattenAfterFill)
                {
                    for (var i = 0; i < form.Fields.Count; i++)
                    {
                        form.Fields[i].ReadOnly = true;
                    }
                }

                // Mitigates blank rendering in viewers (e.g. SumatraPDF, Apple
                // Preview) that don't regenerate appearance streams themselves
                // on open — see ADR-0014.
                form.Elements.SetBoolean("/NeedAppearances", true);

                SaveAtomic(document, request.OutputFile, cancellationToken);
                progress?.Report(new OperationProgress(1.0, "Done"));
                _logger.LogInformation(
                    "Filled {Count} field(s) of {Input} into {Output}",
                    request.Values.Count, request.InputFile, request.OutputFile);
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

    /// <summary>
    /// Recursively converts one level of PDFsharp's outline tree, resolving
    /// each entry's destination page by identity against the open document's
    /// page list (PDFsharp exposes no direct page-index lookup). Depth is
    /// capped, mirroring the guard in <see cref="CollectImageXObjects"/>,
    /// against malformed or cyclic outline dictionaries in hostile files.
    /// </summary>
    private static IReadOnlyList<PdfOutlineNode> BuildOutlineNodes(PdfDocument document, PdfOutlineCollection outlines, int depth)
    {
        if (depth > 64 || outlines.Count == 0)
        {
            return [];
        }

        var nodes = new List<PdfOutlineNode>(outlines.Count);
        foreach (var outline in outlines)
        {
            nodes.Add(new PdfOutlineNode(
                outline.Title,
                FindPageNumber(document, outline.DestinationPage),
                outline.Opened,
                BuildOutlineNodes(document, outline.Outlines, depth + 1)));
        }

        return nodes;
    }

    /// <summary>Finds the 1-based page number of <paramref name="page"/> within <paramref name="document"/>, or null when it isn't a page in this document.</summary>
    private static int? FindPageNumber(PdfDocument document, PdfPage? page)
    {
        if (page is null)
        {
            return null;
        }

        for (var i = 0; i < document.Pages.Count; i++)
        {
            if (ReferenceEquals(document.Pages[i], page))
            {
                return i + 1;
            }
        }

        return null;
    }

    /// <summary>
    /// Local geometry and content-stream bytes for one annotation's normal
    /// appearance, before it is wrapped into a Form XObject and an annotation
    /// dictionary. <see cref="X"/>/<see cref="Y"/>/<see cref="Width"/>/<see cref="Height"/>
    /// are page-space (PDF points, bottom-left origin); <see cref="ContentBytes"/>
    /// is drawn in the appearance's own local space, i.e. relative to (X, Y).
    /// </summary>
    private readonly record struct AnnotationAppearance(
        double X, double Y, double Width, double Height, byte[] ContentBytes, PdfDictionary? Resources);

    /// <summary>
    /// Builds and attaches one annotation to <paramref name="page"/>. PDFsharp
    /// 6.2.4 has no public class for Highlight/FreeText/Ink/Square annotations
    /// (only Link, RubberStamp and the sticky-note-style Text exist, and the
    /// abstract <c>PdfAnnotation</c> base has only a protected constructor) —
    /// see ADR-0013 for the confirmed API inventory this is based on. Every
    /// kind here is therefore hand-authored via the same low-level
    /// <see cref="PdfDictionary"/> API <see cref="TryRecompressImage"/> already
    /// uses to mutate existing objects, extended to originate brand-new ones.
    /// </summary>
    private static void AddAnnotation(PdfDocument document, PdfPage page, AnnotationEdit annotation)
    {
        PdfDictionary annotDict;
        switch (annotation)
        {
            case HighlightAnnotationEdit highlight:
                annotDict = CreateAppearanceAnnotation(document, "/Highlight", BuildHighlightAppearance(document, highlight), highlight.Color, highlight.Opacity);
                annotDict.Elements["/QuadPoints"] = BuildNumberArray(document, highlight.Quads.SelectMany(q => new[] { q.X1, q.Y1, q.X2, q.Y2, q.X3, q.Y3, q.X4, q.Y4 }));
                break;
            case FreeTextAnnotationEdit freeText:
                annotDict = CreateAppearanceAnnotation(document, "/FreeText", BuildFreeTextAppearance(document, freeText), freeText.Color, opacity: 1.0);
                annotDict.Elements.SetString("/Contents", freeText.Text);
                break;
            case InkAnnotationEdit ink:
                annotDict = CreateAppearanceAnnotation(document, "/Ink", BuildInkAppearance(ink), ink.Color, opacity: 1.0);
                annotDict.Elements["/InkList"] = BuildInkListArray(document, ink.Strokes);
                break;
            case SquareAnnotationEdit square:
                annotDict = CreateAppearanceAnnotation(document, "/Square", BuildSquareAppearance(square), square.Color, opacity: 1.0);
                break;
            default:
                throw new PdfOperationException($"Unsupported annotation kind: {annotation.GetType().Name}.");
        }

        page.Annotations.Elements.Add(annotDict);
    }

    /// <summary>
    /// Wraps one appearance as a Form XObject and an annotation dictionary,
    /// registers both as indirect objects (required for the stream, and for
    /// <c>/Annots</c> to reference it by indirect reference the way every
    /// real-world PDF and PDFsharp's own typed annotations do), and returns
    /// the annotation dictionary — not yet attached to any page.
    /// </summary>
    private static PdfDictionary CreateAppearanceAnnotation(
        PdfDocument document, string subtype, AnnotationAppearance appearance, AnnotationColor color, double opacity)
    {
        var formDict = new PdfDictionary(document);
        formDict.Elements.SetName("/Type", "/XObject");
        formDict.Elements.SetName("/Subtype", "/Form");
        formDict.Elements.SetValue("/BBox", new PdfRectangle(new XRect(0, 0, appearance.Width, appearance.Height)));
        if (appearance.Resources is not null)
        {
            formDict.Elements["/Resources"] = appearance.Resources;
        }

        formDict.CreateStream(appearance.ContentBytes);
        document.Internals.AddObject(formDict);

        var annotDict = new PdfDictionary(document);
        annotDict.Elements.SetName("/Type", "/Annot");
        annotDict.Elements.SetName("/Subtype", subtype);
        annotDict.Elements.SetValue("/Rect", new PdfRectangle(new XRect(appearance.X, appearance.Y, appearance.Width, appearance.Height)));
        annotDict.Elements.SetInteger("/F", 4); // Print flag: keeps the annotation visible in print/export paths, not just on-screen.
        annotDict.Elements["/C"] = BuildNumberArray(document, [color.R / 255.0, color.G / 255.0, color.B / 255.0]);
        if (opacity < 1.0)
        {
            annotDict.Elements.SetReal("/CA", opacity);
        }

        var apDict = new PdfDictionary(document);
        apDict.Elements["/N"] = formDict.Reference;
        annotDict.Elements["/AP"] = apDict;
        document.Internals.AddObject(annotDict);

        return annotDict;
    }

    private static AnnotationAppearance BuildSquareAppearance(SquareAnnotationEdit square)
    {
        var inset = square.StrokeWidthPoints / 2;
        var width = Math.Max(0, square.Width - (2 * inset));
        var height = Math.Max(0, square.Height - (2 * inset));

        var sb = new StringBuilder();
        if (square.FillColor is { } fill)
        {
            AppendColorOperator(sb, fill, stroke: false);
            AppendRectOperator(sb, inset, inset, width, height, "f");
        }

        sb.Append(FormatNumber(square.StrokeWidthPoints)).Append(" w\n");
        AppendColorOperator(sb, square.Color, stroke: true);
        AppendRectOperator(sb, inset, inset, width, height, "S");

        return new AnnotationAppearance(square.X, square.Y, square.Width, square.Height, Encoding.ASCII.GetBytes(sb.ToString()), Resources: null);
    }

    private static AnnotationAppearance BuildHighlightAppearance(PdfDocument document, HighlightAnnotationEdit highlight)
    {
        if (highlight.Quads.Count == 0)
        {
            throw new PdfOperationException("A highlight annotation needs at least one quad.");
        }

        var xs = highlight.Quads.SelectMany(q => new[] { q.X1, q.X2, q.X3, q.X4 }).ToList();
        var ys = highlight.Quads.SelectMany(q => new[] { q.Y1, q.Y2, q.Y3, q.Y4 }).ToList();
        var minX = xs.Min();
        var minY = ys.Min();

        var sb = new StringBuilder();
        sb.Append("/GS1 gs\n");
        AppendColorOperator(sb, highlight.Color, stroke: false);
        foreach (var quad in highlight.Quads)
        {
            // Trace the quad TL -> TR -> BR -> BL so the fill covers the full
            // rectangle regardless of the PDF-spec point ordering convention.
            sb.Append(FormatNumber(quad.X1 - minX)).Append(' ').Append(FormatNumber(quad.Y1 - minY)).Append(" m\n");
            sb.Append(FormatNumber(quad.X2 - minX)).Append(' ').Append(FormatNumber(quad.Y2 - minY)).Append(" l\n");
            sb.Append(FormatNumber(quad.X4 - minX)).Append(' ').Append(FormatNumber(quad.Y4 - minY)).Append(" l\n");
            sb.Append(FormatNumber(quad.X3 - minX)).Append(' ').Append(FormatNumber(quad.Y3 - minY)).Append(" l\n");
            sb.Append("h f\n");
        }

        var resources = new PdfDictionary(document);
        var extGStateResources = new PdfDictionary(document);
        var gs1 = new PdfDictionary(document);
        gs1.Elements.SetName("/Type", "/ExtGState");
        gs1.Elements.SetReal("/ca", highlight.Opacity);
        extGStateResources.Elements["/GS1"] = gs1;
        resources.Elements["/ExtGState"] = extGStateResources;

        return new AnnotationAppearance(minX, minY, xs.Max() - minX, ys.Max() - minY, Encoding.ASCII.GetBytes(sb.ToString()), resources);
    }

    private static AnnotationAppearance BuildFreeTextAppearance(PdfDocument document, FreeTextAnnotationEdit freeText)
    {
        const double padding = 4;

        var sb = new StringBuilder();
        AppendColorOperator(sb, freeText.Color, stroke: false);
        sb.Append("BT\n/Helv ").Append(FormatNumber(freeText.FontSize)).Append(" Tf\n");
        sb.Append(FormatNumber(padding)).Append(' ')
          .Append(FormatNumber(Math.Max(0, freeText.Height - freeText.FontSize - padding))).Append(" Td\n");
        sb.Append('(').Append(EscapePdfString(freeText.Text)).Append(") Tj\nET\n");

        var resources = new PdfDictionary(document);
        var fontResources = new PdfDictionary(document);
        var helvetica = new PdfDictionary(document);
        helvetica.Elements.SetName("/Type", "/Font");
        helvetica.Elements.SetName("/Subtype", "/Type1");
        helvetica.Elements.SetName("/BaseFont", "/Helvetica");
        fontResources.Elements["/Helv"] = helvetica;
        resources.Elements["/Font"] = fontResources;

        return new AnnotationAppearance(freeText.X, freeText.Y, freeText.Width, freeText.Height, Encoding.ASCII.GetBytes(sb.ToString()), resources);
    }

    private static AnnotationAppearance BuildInkAppearance(InkAnnotationEdit ink)
    {
        var allPoints = ink.Strokes.SelectMany(s => s.Points).ToList();
        if (allPoints.Count == 0)
        {
            throw new PdfOperationException("An ink annotation needs at least one stroke with at least one point.");
        }

        var half = ink.StrokeWidthPoints / 2;
        var minX = allPoints.Min(p => p.X) - half;
        var minY = allPoints.Min(p => p.Y) - half;
        var maxX = allPoints.Max(p => p.X) + half;
        var maxY = allPoints.Max(p => p.Y) + half;

        var sb = new StringBuilder();
        sb.Append(FormatNumber(ink.StrokeWidthPoints)).Append(" w\n1 J 1 j\n");
        AppendColorOperator(sb, ink.Color, stroke: true);
        foreach (var stroke in ink.Strokes)
        {
            if (stroke.Points.Count == 0)
            {
                continue;
            }

            var first = stroke.Points[0];
            sb.Append(FormatNumber(first.X - minX)).Append(' ').Append(FormatNumber(first.Y - minY)).Append(" m\n");
            foreach (var point in stroke.Points.Skip(1))
            {
                sb.Append(FormatNumber(point.X - minX)).Append(' ').Append(FormatNumber(point.Y - minY)).Append(" l\n");
            }

            sb.Append("S\n");
        }

        return new AnnotationAppearance(minX, minY, maxX - minX, maxY - minY, Encoding.ASCII.GetBytes(sb.ToString()), Resources: null);
    }

    private static void AppendColorOperator(StringBuilder sb, AnnotationColor color, bool stroke)
    {
        sb.Append(FormatNumber(color.R / 255.0)).Append(' ')
          .Append(FormatNumber(color.G / 255.0)).Append(' ')
          .Append(FormatNumber(color.B / 255.0)).Append(' ')
          .Append(stroke ? "RG" : "rg").Append('\n');
    }

    private static void AppendRectOperator(StringBuilder sb, double x, double y, double width, double height, string paintOperator)
    {
        sb.Append(FormatNumber(x)).Append(' ')
          .Append(FormatNumber(y)).Append(' ')
          .Append(FormatNumber(width)).Append(' ')
          .Append(FormatNumber(height)).Append(" re ").Append(paintOperator).Append('\n');
    }

    private static PdfArray BuildNumberArray(PdfDocument document, IEnumerable<double> values)
    {
        var array = new PdfArray(document);
        foreach (var value in values)
        {
            array.Elements.Add(new PdfReal(value));
        }

        return array;
    }

    private static PdfArray BuildInkListArray(PdfDocument document, IReadOnlyList<InkStroke> strokes)
    {
        var outer = new PdfArray(document);
        foreach (var stroke in strokes)
        {
            outer.Elements.Add(BuildNumberArray(document, stroke.Points.SelectMany(p => new[] { p.X, p.Y })));
        }

        return outer;
    }

    /// <summary>
    /// Formats one content-stream number with an invariant decimal point
    /// (content streams are not locale-aware; the current culture's decimal
    /// separator would silently corrupt the stream on many systems).
    /// </summary>
    private static string FormatNumber(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);

    /// <summary>Escapes a string for a PDF literal string operand. Non-ASCII characters are dropped rather than risk corrupting the stream, since the appearance uses an unembedded base-14 font with no custom encoding.</summary>
    private static string EscapePdfString(string text)
    {
        var sb = new StringBuilder(text.Length);
        foreach (var ch in text)
        {
            switch (ch)
            {
                case '(':
                    sb.Append("\\(");
                    break;
                case ')':
                    sb.Append("\\)");
                    break;
                case '\\':
                    sb.Append("\\\\");
                    break;
                case '\r':
                    break;
                case '\n':
                    sb.Append("\\n");
                    break;
                default:
                    sb.Append(ch is >= (char)0x20 and <= (char)0x7E ? ch : '?');
                    break;
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Whether <paramref name="document"/> has an AcroForm at all.
    /// <see cref="PdfDocument.AcroForm"/> throws <see cref="InvalidOperationException"/>
    /// rather than returning null when the catalog has no <c>/AcroForm</c>
    /// entry — verified directly, not assumed — so this checks the catalog
    /// first instead of null-checking the property.
    /// </summary>
    private static bool HasAcroForm(PdfDocument document)
        => document.Internals.Catalog.Elements.ContainsKey("/AcroForm");

    /// <summary>
    /// Maps one PDFsharp typed field to its Domain shape. Radio groups and
    /// list boxes are supported structurally through the same typed API path
    /// as CheckBox/ComboBox but were not exercised against a dedicated
    /// hand-built fixture (radio groups need kid widgets, which is a step
    /// beyond what this milestone's tests build) — see ADR-0014.
    /// </summary>
    private static PdfFormFieldInfo ToFieldInfo(PdfAcroField field)
    {
        return field switch
        {
            PdfTextField text => new PdfFormFieldInfo(text.Name, FormFieldKind.Text, text.ReadOnly, text.Text, null),
            PdfCheckBoxField checkBox => new PdfFormFieldInfo(
                checkBox.Name,
                FormFieldKind.CheckBox,
                checkBox.ReadOnly,
                checkBox.Checked ? checkBox.CheckedName : checkBox.UncheckedName,
                [checkBox.CheckedName, checkBox.UncheckedName]),
            PdfComboBoxField combo => BuildChoiceFieldInfo(combo, FormFieldKind.ComboBox),
            PdfListBoxField listBox => BuildChoiceFieldInfo(listBox, FormFieldKind.ListBox),
            PdfRadioButtonField radio => new PdfFormFieldInfo(
                radio.Name, FormFieldKind.RadioButton, radio.ReadOnly, radio.SelectedIndex.ToString(CultureInfo.InvariantCulture), null),
            PdfSignatureField signature => new PdfFormFieldInfo(signature.Name, FormFieldKind.Signature, IsReadOnly: true, null, null),
            PdfPushButtonField pushButton => new PdfFormFieldInfo(pushButton.Name, FormFieldKind.PushButton, IsReadOnly: true, null, null),
            _ => new PdfFormFieldInfo(field.Name, FormFieldKind.Unsupported, field.ReadOnly, null, null),
        };
    }

    private static PdfFormFieldInfo BuildChoiceFieldInfo(PdfAcroField field, FormFieldKind kind)
    {
        var options = ReadChoiceOptions(field);
        var selectedIndex = field switch
        {
            PdfComboBoxField combo => combo.SelectedIndex,
            PdfListBoxField listBox => listBox.SelectedIndex,
            _ => -1,
        };
        var currentValue = options is not null && selectedIndex >= 0 && selectedIndex < options.Count
            ? options[selectedIndex]
            : null;

        return new PdfFormFieldInfo(field.Name, kind, field.ReadOnly, currentValue, options);
    }

    /// <summary>
    /// Reads a choice field's <c>/Opt</c> array. PDFsharp's typed choice field
    /// classes expose <c>SelectedIndex</c> but no public list of the options
    /// themselves, so this reads the raw array the same way
    /// <see cref="TryRecompressImage"/> reads other low-level dictionary data.
    /// </summary>
    private static IReadOnlyList<string>? ReadChoiceOptions(PdfAcroField field)
    {
        if (field.Elements["/Opt"] is not PdfArray options)
        {
            return null;
        }

        var result = new List<string>(options.Elements.Count);
        for (var i = 0; i < options.Elements.Count; i++)
        {
            result.Add(options.Elements[i] switch
            {
                PdfString exportValue => exportValue.Value,
                PdfArray { Elements.Count: > 1 } pair when pair.Elements[1] is PdfString display => display.Value,
                var other => other?.ToString() ?? string.Empty,
            });
        }

        return result;
    }

    /// <summary>Finds one field by its fully qualified name. PDFsharp exposes no by-name lookup on the public collection type, so this scans.</summary>
    private static PdfAcroField? FindField(PdfAcroForm form, string fullyQualifiedName)
    {
        for (var i = 0; i < form.Fields.Count; i++)
        {
            var field = form.Fields[i];
            if (field.Name == fullyQualifiedName)
            {
                return field;
            }
        }

        return null;
    }

    private static void SetFieldValue(PdfAcroField field, string value)
    {
        switch (field)
        {
            case PdfTextField text:
                text.Text = value;
                break;
            case PdfCheckBoxField checkBox:
                checkBox.Checked = string.Equals(value, checkBox.CheckedName, StringComparison.Ordinal);
                break;
            case PdfComboBoxField combo:
                combo.SelectedIndex = ResolveChoiceIndex(combo, value);
                break;
            case PdfListBoxField listBox:
                listBox.SelectedIndex = ResolveChoiceIndex(listBox, value);
                break;
            case PdfRadioButtonField radio:
                if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var radioIndex))
                {
                    throw new PdfOperationException($"Radio field \"{field.Name}\" needs a numeric option index, got \"{value}\".");
                }

                radio.SelectedIndex = radioIndex;
                break;
            default:
                throw new PdfOperationException($"Field \"{field.Name}\" is not fillable ({field.GetType().Name}).");
        }
    }

    private static int ResolveChoiceIndex(PdfAcroField field, string value)
    {
        var options = ReadChoiceOptions(field);
        var index = options?.ToList().IndexOf(value) ?? -1;
        if (index < 0)
        {
            throw new PdfOperationException($"\"{value}\" is not a valid option for field \"{field.Name}\".");
        }

        return index;
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

