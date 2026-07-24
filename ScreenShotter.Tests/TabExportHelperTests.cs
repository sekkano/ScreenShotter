using System.Drawing;
using System.Drawing.Imaging;
using Xunit;

namespace ScreenShotter.Tests;

/// <summary>
/// Exercises real TabExportHelper used for current-tab composite save.
/// </summary>
public class TabExportHelperTests
{
    [Fact]
    public void ComputeUnionBounds_Empty_ReturnsEmpty()
    {
        var union = TabExportHelper.ComputeUnionBounds(Array.Empty<Rectangle>());
        Assert.Equal(Rectangle.Empty, union);
    }

    [Fact]
    public void ComputeUnionBounds_MultipleFrames_IncludesAll()
    {
        var a = new Rectangle(10, 20, 100, 50);
        var b = new Rectangle(80, 40, 60, 90); // overlaps / extends
        var union = TabExportHelper.ComputeUnionBounds(new[] { a, b });

        Assert.Equal(new Rectangle(10, 20, 130, 110), union);
    }

    [Fact]
    public void FrameInExport_OffsetsToUnionOrigin()
    {
        var union = new Rectangle(10, 20, 200, 100);
        var frame = new Rectangle(30, 40, 50, 25);
        var dest = TabExportHelper.FrameInExport(union, frame);

        Assert.Equal(new Rectangle(20, 20, 50, 25), dest);
    }

    [Theory]
    [InlineData("out.png", "Png")]
    [InlineData("shot.JPG", "Jpeg")]
    [InlineData("x.jpeg", "Jpeg")]
    [InlineData("x.bmp", "Bmp")]
    [InlineData("noext", "Png")]
    public void FormatFromPath_MapsExtension(string path, string expectedName)
    {
        var format = TabExportHelper.FormatFromPath(path);
        Assert.Equal(expectedName, format.ToString());
    }

    [Fact]
    public void IsValidSavePath_RejectsEmpty_AcceptsFileName()
    {
        Assert.False(TabExportHelper.IsValidSavePath(""));
        Assert.False(TabExportHelper.IsValidSavePath("   "));
        Assert.True(TabExportHelper.IsValidSavePath("tab_export.png"));
    }

    [Fact]
    public void BottomToTopControlIndices_PaintsFrontLast()
    {
        // WinForms: index 0 = front (top). Paint order must end with 0.
        var indices = TabExportHelper.BottomToTopControlIndices(3);
        Assert.Equal(new[] { 2, 1, 0 }, indices);
        Assert.Empty(TabExportHelper.BottomToTopControlIndices(0));
        Assert.Equal(new[] { 0 }, TabExportHelper.BottomToTopControlIndices(1));
    }
}
