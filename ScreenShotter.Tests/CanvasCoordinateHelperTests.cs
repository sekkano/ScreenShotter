using System.Drawing;
using Xunit;

namespace ScreenShotter.Tests;

public class CanvasCoordinateHelperTests
{
    [Fact]
    public void ClientToDocument_AccountsForNegativeAutoScrollPosition()
    {
        // Scrolled right/down by 100,50 → AutoScrollPosition getter is (-100, -50)
        var client = new Point(10, 20);
        var asp = new Point(-100, -50);
        var doc = CanvasCoordinateHelper.ClientToDocument(client, asp);

        Assert.Equal(new Point(110, 70), doc);
    }

    [Fact]
    public void DocumentToClient_RoundTrips()
    {
        var asp = new Point(-80, -40);
        var doc = new Point(200, 150);
        var client = CanvasCoordinateHelper.DocumentToClient(doc, asp);
        Assert.Equal(doc, CanvasCoordinateHelper.ClientToDocument(client, asp));
    }

    [Fact]
    public void ComputeScrollMinSize_IncludesResizedFrameExtents()
    {
        var frames = new[]
        {
            new Rectangle(20, 20, 100, 80),
            new Rectangle(500, 100, 800, 600), // large / far
        };
        var size = CanvasCoordinateHelper.ComputeScrollMinSize(frames, new Size(400, 300), pad: 80);

        Assert.True(size.Width >= 500 + 800 + 80);
        Assert.True(size.Height >= 100 + 600 + 80);
        Assert.True(size.Width >= 400);
        Assert.True(size.Height >= 300);
    }

    [Fact]
    public void ClampDocumentLocation_PreventsNegativeOrigin()
    {
        var clamped = CanvasCoordinateHelper.ClampDocumentLocation(new Point(-50, -10), new Size(200, 100));
        Assert.Equal(0, clamped.X);
        Assert.Equal(0, clamped.Y);
    }
}
