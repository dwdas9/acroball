using Acroball.Infrastructure.Pdf;
using Xunit;

namespace Acroball.Infrastructure.Tests;

public class OutputNameTemplateTests
{
    [Fact]
    public void Expands_all_tokens()
        => Assert.Equal(
            "report-2-5-10.pdf",
            OutputNameTemplate.Expand("{name}-{index}-{range}", "report", 2, "5-10"));

    [Fact]
    public void Appends_pdf_extension_when_missing()
        => Assert.Equal("report-1.pdf", OutputNameTemplate.Expand("{name}-{index}", "report", 1, "1"));

    [Fact]
    public void Does_not_double_extension()
        => Assert.Equal("part.pdf", OutputNameTemplate.Expand("part.pdf", "x", 1, "1"));

    [Fact]
    public void Replaces_invalid_file_name_characters()
    {
        var result = OutputNameTemplate.Expand("{name}-{index}", "a/b:c", 1, "1");
        Assert.DoesNotContain('/', result);
        Assert.DoesNotContain(':', result);
        Assert.EndsWith(".pdf", result);
    }

    [Fact]
    public void Rejects_empty_template()
        => Assert.Throws<ArgumentException>(() => OutputNameTemplate.Expand("  ", "x", 1, "1"));
}

