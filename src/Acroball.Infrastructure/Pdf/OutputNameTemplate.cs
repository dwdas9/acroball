namespace Acroball.Infrastructure.Pdf;

/// <summary>
/// Expands split-output file name templates. Pure string logic, kept out of
/// the engine so it is trivially unit-testable.
/// </summary>
/// <remarks>
/// Supported tokens: <c>{name}</c> (source file name without extension),
/// <c>{index}</c> (1-based range index), <c>{range}</c> (the range in its
/// compact text form, e.g. <c>2-5</c>). Characters invalid in file names are
/// replaced with <c>_</c> and a <c>.pdf</c> extension is guaranteed.
/// </remarks>
public static class OutputNameTemplate
{
    /// <summary>Expands <paramref name="template"/> for one split output.</summary>
    /// <param name="template">The template, e.g. <c>"{name}-{index}"</c>.</param>
    /// <param name="sourceName">Source file name without extension.</param>
    /// <param name="index">1-based index of the range.</param>
    /// <param name="rangeText">Compact range text, e.g. <c>"2-5"</c>.</param>
    public static string Expand(string template, string sourceName, int index, string rangeText)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(template);

        var name = template
            .Replace("{name}", sourceName, StringComparison.Ordinal)
            .Replace("{index}", index.ToString(), StringComparison.Ordinal)
            .Replace("{range}", rangeText, StringComparison.Ordinal);

        foreach (var invalid in Path.GetInvalidFileNameChars())
        {
            name = name.Replace(invalid, '_');
        }

        return name.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase) ? name : name + ".pdf";
    }
}

