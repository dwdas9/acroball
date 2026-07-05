using Acroball.Application.Operations;
using Acroball.Domain;

namespace Acroball.Application.Abstractions;

/// <summary>
/// The manipulation backend for PDF files: everything that reads or rewrites
/// documents without rasterizing them. Implementations must be safe to call
/// from any thread and must perform file I/O off the caller's synchronization
/// context.
/// </summary>
/// <remarks>
/// Rendering (thumbnails, previews) deliberately lives on a separate
/// abstraction, <see cref="IPdfRenderService"/>, because it is served by a
/// different native backend with different threading rules. See ADR-0002.
/// Failures surface as <see cref="Acroball.Domain.Exceptions.PdfOperationException"/>
/// subtypes; cancellation surfaces as <see cref="OperationCanceledException"/>.
/// </remarks>
public interface IPdfEngine
{
    /// <summary>Reads summary information about a PDF without loading its pages.</summary>
    Task<PdfDocumentInfo> InspectAsync(
        string filePath,
        string? password = null,
        CancellationToken cancellationToken = default);

    /// <summary>Reads per-page geometry for a PDF.</summary>
    Task<IReadOnlyList<PdfPageInfo>> GetPagesAsync(
        string filePath,
        string? password = null,
        CancellationToken cancellationToken = default);

    /// <summary>Merges several documents into one. See <see cref="MergeRequest"/>.</summary>
    Task MergeAsync(
        MergeRequest request,
        IProgress<OperationProgress>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>Splits a document into several files. See <see cref="SplitRequest"/>.</summary>
    Task SplitAsync(
        SplitRequest request,
        IProgress<OperationProgress>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>Extracts pages into a new document. See <see cref="ExtractPagesRequest"/>.</summary>
    Task ExtractPagesAsync(
        ExtractPagesRequest request,
        IProgress<OperationProgress>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>Rotates pages. See <see cref="RotatePagesRequest"/>.</summary>
    Task RotatePagesAsync(
        RotatePagesRequest request,
        IProgress<OperationProgress>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>Assembles a document from an explicit page list. See <see cref="ComposeRequest"/>.</summary>
    Task ComposeAsync(
        ComposeRequest request,
        IProgress<OperationProgress>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>Encrypts a document. See <see cref="EncryptRequest"/>.</summary>
    Task EncryptAsync(
        EncryptRequest request,
        IProgress<OperationProgress>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>Removes encryption from a document. See <see cref="DecryptRequest"/>.</summary>
    Task DecryptAsync(
        DecryptRequest request,
        IProgress<OperationProgress>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>Rewrites a document to reduce its size. See <see cref="CompressRequest"/>.</summary>
    Task CompressAsync(
        CompressRequest request,
        IProgress<OperationProgress>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>Replaces document metadata. See <see cref="UpdateMetadataRequest"/>.</summary>
    Task UpdateMetadataAsync(
        UpdateMetadataRequest request,
        IProgress<OperationProgress>? progress = null,
        CancellationToken cancellationToken = default);
}

