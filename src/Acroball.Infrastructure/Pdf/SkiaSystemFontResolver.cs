using PdfSharp.Fonts;
using SkiaSharp;

namespace Acroball.Infrastructure.Pdf;

/// <summary>
/// Resolves fonts for PDFsharp by extracting real font file bytes from
/// whatever the OS has installed, via SkiaSharp's cross-platform font
/// manager (already a dependency, ADR-0009). Existence, not correctness — see
/// ADR-0014.
/// </summary>
/// <remarks>
/// PDFsharp 6.x ships no default <see cref="IFontResolver"/>, and merely
/// <em>reading</em> an existing <c>PdfTextField</c> (not just drawing one)
/// makes PDFsharp eagerly construct an internal font, unconditionally, even
/// when the field's own <c>/DA</c> already names a standard font. Without a
/// resolver assigned, that throws for every text field, in every PDF, on
/// every platform. Acroball never draws form-field text through PDFsharp's
/// own font/graphics API — every text this app actually renders (annotation
/// content streams, ADR-0013) is written by hand — so resolution accuracy
/// (matching the exact requested family) doesn't matter here: any real,
/// loadable font satisfies PDFsharp's internal requirement.
/// </remarks>
public sealed class SkiaSystemFontResolver : IFontResolver
{
    /// <inheritdoc />
    public byte[]? GetFont(string faceName)
    {
        using var typeface = SKTypeface.FromFamilyName(faceName) ?? SKFontManager.Default.MatchFamily(familyName: null);
        if (typeface is null)
        {
            return null;
        }

        using var stream = typeface.OpenStream(out _);
        if (stream is null)
        {
            return null;
        }

        var bytes = new byte[stream.Length];
        stream.Read(bytes, bytes.Length);
        return bytes;
    }

    /// <inheritdoc />
    public FontResolverInfo ResolveTypeface(string familyName, bool bold, bool italic) => new(familyName);
}
