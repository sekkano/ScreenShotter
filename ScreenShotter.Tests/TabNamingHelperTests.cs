using Xunit;

namespace ScreenShotter.Tests;

public class TabNamingHelperTests
{
    [Theory]
    [InlineData(0, "Tab 1")]
    [InlineData(1, "Tab 2")]
    [InlineData(2, "Tab 3")]
    public void NextDefaultTabName_FromExistingCount(int existing, string expected)
    {
        Assert.Equal(expected, TabNamingHelper.NextDefaultTabName(existing));
    }

    [Fact]
    public void NormalizeTabName_Trims_RejectsBlank()
    {
        Assert.Equal("Hello", TabNamingHelper.NormalizeTabName("  Hello  "));
        Assert.Null(TabNamingHelper.NormalizeTabName(""));
        Assert.Null(TabNamingHelper.NormalizeTabName("   "));
        Assert.Null(TabNamingHelper.NormalizeTabName(null));
    }
}
