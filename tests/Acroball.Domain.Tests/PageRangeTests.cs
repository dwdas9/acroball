using Acroball.Domain;
using Xunit;

namespace Acroball.Domain.Tests;

public class PageRangeTests
{
    // ---- construction ----

    [Fact]
    public void Constructor_rejects_start_below_one()
        => Assert.Throws<ArgumentOutOfRangeException>(() => new PageRange(0, 5));

    [Fact]
    public void Constructor_rejects_end_before_start()
        => Assert.Throws<ArgumentOutOfRangeException>(() => new PageRange(5, 2));

    [Fact]
    public void Enumerate_yields_all_pages_in_order()
        => Assert.Equal(new[] { 3, 4, 5 }, new PageRange(3, 5).Enumerate());

    [Theory]
    [InlineData(1, 1, "1")]
    [InlineData(2, 6, "2-6")]
    public void ToString_uses_compact_form(int start, int end, string expected)
        => Assert.Equal(expected, new PageRange(start, end).ToString());

    // ---- parsing: valid input ----

    [Fact]
    public void Parses_single_page()
    {
        Assert.True(PageRange.TryParseList("5", 10, out var ranges, out var error));
        Assert.Null(error);
        Assert.Equal([new PageRange(5, 5)], ranges);
    }

    [Fact]
    public void Parses_simple_range()
    {
        Assert.True(PageRange.TryParseList("2-6", 10, out var ranges, out _));
        Assert.Equal([new PageRange(2, 6)], ranges);
    }

    [Fact]
    public void Parses_open_range_to_last_page()
    {
        Assert.True(PageRange.TryParseList("7-", 10, out var ranges, out _));
        Assert.Equal([new PageRange(7, 10)], ranges);
    }

    [Fact]
    public void Parses_full_document_as_open_range()
    {
        Assert.True(PageRange.TryParseList("1-", 10, out var ranges, out _));
        Assert.Equal([new PageRange(1, 10)], ranges);
    }

    [Fact]
    public void Parses_mixed_list_with_whitespace()
    {
        Assert.True(PageRange.TryParseList(" 1-3 , 5 , 7 - 9 ", 10, out var ranges, out _));
        Assert.Equal([new PageRange(1, 3), new PageRange(5, 5), new PageRange(7, 9)], ranges);
    }

    [Fact]
    public void Preserves_user_order()
    {
        Assert.True(PageRange.TryParseList("3,1", 10, out var ranges, out _));
        Assert.Equal([new PageRange(3, 3), new PageRange(1, 1)], ranges);
    }

    [Fact]
    public void Preserves_duplicates()
    {
        Assert.True(PageRange.TryParseList("2,2", 10, out var ranges, out _));
        Assert.Equal([new PageRange(2, 2), new PageRange(2, 2)], ranges);
    }

    [Fact]
    public void Accepts_last_page_exactly()
    {
        Assert.True(PageRange.TryParseList("10", 10, out var ranges, out _));
        Assert.Equal([new PageRange(10, 10)], ranges);
    }

    // ---- parsing: invalid input ----

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("0")]
    [InlineData("abc")]
    [InlineData("5-2")]
    [InlineData("11")]
    [InlineData("3-12")]
    [InlineData("-3")]
    [InlineData("1,,2")]
    [InlineData("1,")]
    public void Rejects_invalid_input_with_error(string? text)
    {
        Assert.False(PageRange.TryParseList(text, 10, out var ranges, out var error));
        Assert.Empty(ranges);
        Assert.False(string.IsNullOrWhiteSpace(error));
    }

    [Fact]
    public void Rejects_empty_document()
    {
        Assert.False(PageRange.TryParseList("1", 0, out _, out var error));
        Assert.False(string.IsNullOrWhiteSpace(error));
    }

    [Fact]
    public void Contains_is_inclusive()
    {
        var range = new PageRange(2, 4);
        Assert.True(range.Contains(2));
        Assert.True(range.Contains(4));
        Assert.False(range.Contains(1));
        Assert.False(range.Contains(5));
    }

    [Fact]
    public void Count_is_inclusive()
        => Assert.Equal(3, new PageRange(2, 4).Count);
}

