using System.Drawing;
using Xunit;

namespace ScreenShotter.Tests;

public class DrawingHelperTests
{
    [Fact]
    public void ViewportToNormalized_Center_IsHalf()
    {
        var frame = new Size(200, 100);
        var pan = new Point(0, 0);
        // zoom 1 → content = frame
        var norm = DrawingHelper.ViewportToNormalized(new Point(100, 50), frame, pan, 1.0);
        Assert.NotNull(norm);
        Assert.Equal(0.5f, norm!.Value.X, 3);
        Assert.Equal(0.5f, norm.Value.Y, 3);
    }

    [Fact]
    public void ViewportToNormalized_OutsideImage_ReturnsNull()
    {
        var frame = new Size(100, 100);
        // pan leaves empty margins when zoomed out? at zoom 1 content fills frame
        var miss = DrawingHelper.ViewportToNormalized(new Point(-5, 10), frame, Point.Empty, 1.0);
        Assert.Null(miss);
    }

    [Fact]
    public void NormalizedToViewport_RoundTripsCenter()
    {
        var frame = new Size(200, 100);
        var pan = new Point(10, 5);
        var zoom = 2.0;
        var back = DrawingHelper.NormalizedToViewport(new PointF(0.5f, 0.5f), frame, pan, zoom);
        var content = ZoomHelper.ContentSize(frame, zoom);
        Assert.Equal(pan.X + 0.5f * content.Width, back.X, 2);
        Assert.Equal(pan.Y + 0.5f * content.Height, back.Y, 2);
    }

    [Fact]
    public void CreateHighlighterStroke_HasTranslucentYellow()
    {
        var stroke = DrawingHelper.CreateHighlighterStroke();
        Assert.Equal(DrawingTool.Highlighter, stroke.Tool);
        Assert.True(stroke.Color.A < 255);
        Assert.True(stroke.NativeWidth >= 1);
    }

    [Fact]
    public void ViewportStrokeWidth_ScalesWithZoom()
    {
        var natural = new Size(100, 100);
        var frame = new Size(100, 100);
        var at1 = DrawingHelper.ViewportStrokeWidth(20, frame, natural, 1.0);
        var at2 = DrawingHelper.ViewportStrokeWidth(20, frame, natural, 2.0);
        Assert.True(at2 > at1);
    }
}
