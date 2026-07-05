namespace Acroball.Domain;

/// <summary>
/// An inclusive, 1-based range of pages within a PDF document.
/// </summary>
/// <remarks>
/// Ranges are parsed from the familiar syntax <c>"1-3, 5, 7-"</c> where a
/// trailing dash means "to the last page". Parsing deliberately preserves the
/// order and duplicates supplied by the user: <c>"3,1,1"</c> is a valid
/// instruction (e.g. for extraction) and is not normalized away.
/// </remarks>
public readonly record struct PageRange
{
    /// <summary>First page of the range (1-based, inclusive).</summary>
    public int Start { get; }

    /// <summary>Last page of the range (1-based, inclusive).</summary>
    public int End { get; }

    /// <summary>Creates a range from <paramref name="start"/> to <paramref name="end"/>, inclusive.</summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="start"/> is less than 1 or <paramref name="end"/> is less than <paramref name="start"/>.
    /// </exception>
    public PageRange(int start, int end)
    {
        if (start < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(start), start, "Page numbers are 1-based.");
        }

        if (end < start)
        {
            throw new ArgumentOutOfRangeException(nameof(end), end, "End page must be greater than or equal to start page.");
        }

        Start = start;
        End = end;
    }

    /// <summary>Creates a range covering a single page.</summary>
    public static PageRange Single(int page) => new(page, page);

    /// <summary>Number of pages covered by this range.</summary>
    public int Count => End - Start + 1;

    /// <summary>Returns <see langword="true"/> when <paramref name="page"/> falls inside this range.</summary>
    public bool Contains(int page) => page >= Start && page <= End;

    /// <summary>Enumerates every page number in the range, in ascending order.</summary>
    public IEnumerable<int> Enumerate()
    {
        for (var page = Start; page <= End; page++)
        {
            yield return page;
        }
    }

    /// <inheritdoc />
    public override string ToString() => Start == End ? Start.ToString() : $"{Start}-{End}";

    /// <summary>
    /// Parses a comma-separated page range expression such as <c>"1-3, 5, 7-"</c>.
    /// </summary>
    /// <param name="text">The expression to parse. May contain whitespace around tokens.</param>
    /// <param name="documentPageCount">Total pages in the target document; used to resolve open ranges and validate bounds.</param>
    /// <param name="ranges">The parsed ranges, in the order written, when parsing succeeds.</param>
    /// <param name="error">A human-readable reason when parsing fails; otherwise <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when the whole expression parsed and validated.</returns>
    public static bool TryParseList(
        string? text,
        int documentPageCount,
        out IReadOnlyList<PageRange> ranges,
        out string? error)
    {
        ranges = Array.Empty<PageRange>();

        if (documentPageCount < 1)
        {
            error = "The document has no pages.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            error = "Enter at least one page or range, e.g. \"1-3, 5\".";
            return false;
        }

        var result = new List<PageRange>();

        foreach (var rawToken in text.Split(','))
        {
            var token = rawToken.Trim();
            if (token.Length == 0)
            {
                error = "Empty entry between commas.";
                return false;
            }

            var dashIndex = token.IndexOf('-');
            if (dashIndex < 0)
            {
                // Single page: "5"
                if (!TryParsePageNumber(token, documentPageCount, out var page, out error))
                {
                    return false;
                }

                result.Add(Single(page));
                continue;
            }

            var startText = token[..dashIndex].Trim();
            var endText = token[(dashIndex + 1)..].Trim();

            if (startText.Length == 0)
            {
                error = $"\"{token}\" is not a valid range. Ranges start with a page number, e.g. \"2-5\".";
                return false;
            }

            if (!TryParsePageNumber(startText, documentPageCount, out var start, out error))
            {
                return false;
            }

            int end;
            if (endText.Length == 0)
            {
                // Open range: "7-" means "to the last page".
                end = documentPageCount;
            }
            else if (!TryParsePageNumber(endText, documentPageCount, out end, out error))
            {
                return false;
            }

            if (end < start)
            {
                error = $"Range \"{token}\" runs backwards; the first page must not be greater than the last.";
                return false;
            }

            result.Add(new PageRange(start, end));
        }

        ranges = result;
        error = null;
        return true;
    }

    private static bool TryParsePageNumber(string token, int documentPageCount, out int page, out string? error)
    {
        if (!int.TryParse(token, out page))
        {
            error = $"\"{token}\" is not a page number.";
            return false;
        }

        if (page < 1)
        {
            error = "Page numbers start at 1.";
            return false;
        }

        if (page > documentPageCount)
        {
            error = $"Page {page} is beyond the last page ({documentPageCount}).";
            return false;
        }

        error = null;
        return true;
    }
}

