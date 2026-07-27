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
        var norm = DrawingHelper.ViewportToNormalized(new Point(100, 50), frame, pan, 1.0);
        Assert.NotNull(norm);
        Assert.Equal(0.5f, norm!.Value.X, 3);
        Assert.Equal(0.5f, norm.Value.Y, 3);
    }

    [Fact]
    public void ViewportToNormalized_OutsideImage_ReturnsNull()
    {
        var frame = new Size(100, 100);
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
    public void DrawingSettings_CreateStroke_UsesColorOpacityThickness()
    {
        var settings = new DrawingSettings
        {
            Tool = DrawingTool.Highlighter,
            BaseColor = Color.FromArgb(255, 0, 128, 255),
            OpacityPercent = 50,
            Thickness = 20
        };
        var stroke = settings.CreateStroke();
        Assert.Equal(DrawingTool.Highlighter, stroke.Tool);
        Assert.Equal(20f, stroke.NativeWidth);
        Assert.Equal(128, stroke.Color.A); // 50% of 255
        Assert.Equal(0, stroke.Color.R);
        Assert.Equal(128, stroke.Color.G);
        Assert.Equal(255, stroke.Color.B);
    }

    [Fact]
    public void ColorWithOpacity_ClampsAndAppliesAlpha()
    {
        var c = DrawingHelper.ColorWithOpacity(Color.Red, 25);
        Assert.Equal(64, c.A); // ~0.25 * 255
        Assert.Equal(255, c.R);
    }

    [Fact]
    public void IsInkTool_OnlyDrawTools()
    {
        Assert.False(DrawingHelper.IsInkTool(DrawingTool.Pointer));
        Assert.True(DrawingHelper.IsInkTool(DrawingTool.Highlighter));
        Assert.True(DrawingHelper.IsInkTool(DrawingTool.Pen));
    }

    [Fact]
    public void ClampThickness_EnforcesRange()
    {
        Assert.Equal(DrawingHelper.MinThickness, DrawingHelper.ClampThickness(0));
        Assert.Equal(DrawingHelper.MaxThickness, DrawingHelper.ClampThickness(500));
        Assert.Equal(28f, DrawingHelper.ClampThickness(28));
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
