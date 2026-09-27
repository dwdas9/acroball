using Acroball.UI.Tools;
using Xunit;

namespace Acroball.UI.Tests;

public sealed class ToolCatalogTests
{
    // Characters that only appear when UTF-8 text has been re-read as
    // Windows-1252 and saved again (e.g. "°" becoming "Â°").
    private static readonly string[] MojibakeMarkers = ["Ã", "Â", "â€"];

    [Fact]
    public void Tool_labels_and_descriptions_are_free_of_double_encoded_text()
    {
        foreach (var tool in ToolCatalog.All)
        {
            foreach (var marker in MojibakeMarkers)
            {
                Assert.DoesNotContain(marker, tool.Label, StringComparison.Ordinal);
                Assert.DoesNotContain(marker, tool.Description, StringComparison.Ordinal);
            }
        }
    }

    [Fact]
    public void Rotate_description_uses_a_real_degree_sign()
    {
        Assert.Contains("90°", ToolCatalog.GetById("rotate").Description, StringComparison.Ordinal);
    }

    [Fact]
    public void Tool_ids_are_unique()
    {
        var ids = ToolCatalog.All.Select(t => t.Id).ToList();

        Assert.Equal(ids.Count, ids.Distinct().Count());
    }
}
