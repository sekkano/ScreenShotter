using System.Drawing;
using Xunit;

namespace ScreenShotter.Tests;

public class ScreenshotLayoutHelperTests
{
    [Fact]
    public void PlaceNext_FirstScreenshot_UsesMargin()
    {
        var p = ScreenshotLayoutHelper.PlaceNextScreenshot(
            Array.Empty<Rectangle>(),
            new Size(100, 80),
            origin: Point.Empty);

        Assert.Equal(new Point(20, 20), p);
    }

    [Fact]
    public void PlaceNext_SecondScreenshot_GoesToTheRight()
    {
        var existing = new[] { new Rectangle(20, 20, 100, 80) };
        var p = ScreenshotLayoutHelper.PlaceNextScreenshot(
            existing,
            new Size(100, 80),
            origin: Point.Empty,
            gap: 16);

        Assert.Equal(new Point(20 + 100 + 16, 20), p);
    }

    [Fact]
    public void PlaceNext_WrapsToNewRow_WhenTooWide()
    {
        var existing = new[]
        {
            new Rectangle(20, 20, 900, 100),
            new Rectangle(936, 20, 900, 100),
        };
        var p = ScreenshotLayoutHelper.PlaceNextScreenshot(
            existing,
            new Size(900, 100),
            origin: Point.Empty,
            gap: 16,
            preferredRowWidth: 1600);

        // Should drop below max bottom (120), not continue the wide row
        Assert.Equal(20, p.X);
        Assert.Equal(20 + 100 + 16, p.Y);
    }

    [Fact]
    public void PlaceNext_DoesNotStackOnSameOrigin()
    {
        var a = new Rectangle(20, 20, 200, 150);
        var bLoc = ScreenshotLayoutHelper.PlaceNextScreenshot(new[] { a }, new Size(200, 150));
        var b = new Rectangle(bLoc, new Size(200, 150));
        var cLoc = ScreenshotLayoutHelper.PlaceNextScreenshot(new[] { a, b }, new Size(200, 150));

        Assert.NotEqual(a.Location, bLoc);
        Assert.NotEqual(bLoc, cLoc);
        Assert.True(bLoc.X > a.Right || bLoc.Y > a.Bottom);
    }
}
