using System.Drawing;
using Xunit;

namespace ScreenShotter.Tests;

public class ScreenshotLayoutHelperTests
{
    [Fact]
    public void PlaceNext_FirstScreenshot_UsesMarginInViewport()
    {
        var p = ScreenshotLayoutHelper.PlaceNextScreenshot(
            Array.Empty<Rectangle>(),
            new Size(100, 80),
            viewportOrigin: Point.Empty,
            viewportWidth: 400);

        Assert.Equal(new Point(20, 20), p);
    }

    [Fact]
    public void PlaceNext_FirstScreenshot_UsesScrolledViewportOrigin()
    {
        var p = ScreenshotLayoutHelper.PlaceNextScreenshot(
            Array.Empty<Rectangle>(),
            new Size(100, 80),
            viewportOrigin: new Point(200, 100),
            viewportWidth: 400);

        Assert.Equal(new Point(220, 120), p);
    }

    [Fact]
    public void PlaceNext_SecondScreenshot_GoesToTheRight_WhenViewportIsWide()
    {
        var existing = new[] { new Rectangle(20, 20, 100, 80) };
        var p = ScreenshotLayoutHelper.PlaceNextScreenshot(
            existing,
            new Size(100, 80),
            viewportOrigin: Point.Empty,
            viewportWidth: 800,
            gap: 16);

        Assert.Equal(new Point(20 + 100 + 16, 20), p);
    }

    [Fact]
    public void PlaceNext_WrapsBelow_WhenWindowIsNarrow()
    {
        // Window ~1/4 of a typical desktop: next snip would start past the right edge
        var existing = new[] { new Rectangle(20, 20, 280, 150) };
        var p = ScreenshotLayoutHelper.PlaceNextScreenshot(
            existing,
            new Size(400, 200),
            viewportOrigin: Point.Empty,
            viewportWidth: 320,
            gap: 16,
            minVisibleEdge: 48);

        // 20+280+16 = 316; 316+48 = 364 > 320 → wrap under
        Assert.Equal(20, p.X);
        Assert.Equal(20 + 150 + 16, p.Y);
    }

    [Fact]
    public void PlaceNext_PlacesBeside_WhenLeftEdgeStillInView_EvenIfImageExtendsPast()
    {
        // New image is wider than remaining space, but its left edge is still on-screen
        var existing = new[] { new Rectangle(20, 20, 100, 80) };
        var p = ScreenshotLayoutHelper.PlaceNextScreenshot(
            existing,
            new Size(500, 200),
            viewportOrigin: Point.Empty,
            viewportWidth: 400,
            gap: 16,
            minVisibleEdge: 48);

        // 20+100+16 = 136; 136+48 = 184 <= 400 → stay on row (image may clip off the right)
        Assert.Equal(new Point(136, 20), p);
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
            viewportOrigin: Point.Empty,
            viewportWidth: 1600,
            gap: 16);

        // 936+900+16 = 1852; left edge past 1600 → drop below
        Assert.Equal(20, p.X);
        Assert.Equal(20 + 100 + 16, p.Y);
    }

    [Fact]
    public void PlaceNext_DoesNotStackOnSameOrigin()
    {
        var a = new Rectangle(20, 20, 200, 150);
        var bLoc = ScreenshotLayoutHelper.PlaceNextScreenshot(
            new[] { a },
            new Size(200, 150),
            viewportWidth: 1200);
        var b = new Rectangle(bLoc, new Size(200, 150));
        var cLoc = ScreenshotLayoutHelper.PlaceNextScreenshot(
            new[] { a, b },
            new Size(200, 150),
            viewportWidth: 1200);

        Assert.NotEqual(a.Location, bLoc);
        Assert.NotEqual(bLoc, cLoc);
        Assert.True(bLoc.X > a.Right || bLoc.Y > a.Bottom);
    }

    [Fact]
    public void PlaceNext_LegacyOverload_StillWrapsWithPreferredRowWidth()
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

        Assert.Equal(20, p.X);
        Assert.Equal(20 + 100 + 16, p.Y);
    }
}
