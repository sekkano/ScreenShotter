using System.Drawing;
using Xunit;

namespace ScreenShotter.Tests;

/// <summary>
/// Exercises real ZoomHelper — fixed-frame zoom, free vs aspect resize, pan clamp.
/// </summary>
public class ZoomHelperTests
{
    [Fact]
    public void DisplaySize_AtDefaultZoom_EqualsNativeSize()
    {
        var natural = new Size(1920, 1080);
        var display = ZoomHelper.DisplaySize(natural, ZoomHelper.DefaultZoom);

        Assert.Equal(1920, display.Width);
        Assert.Equal(1080, display.Height);
    }

    [Fact]
    public void DisplaySize_DoesNotCapLargeScreenshots()
    {
        var natural = new Size(3840, 2160);
        var display = ZoomHelper.DisplaySize(natural, 1.0);

        Assert.Equal(3840, display.Width);
        Assert.Equal(2160, display.Height);
    }

    [Fact]
    public void ContentSize_ZoomDoesNotDependOnNative_OnlyFrameAndZoom()
    {
        // Frame stays fixed; content grows with zoom (viewport model)
        var frame = new Size(800, 600);
        var at1 = ZoomHelper.ContentSize(frame, 1.0);
        var at2 = ZoomHelper.ContentSize(frame, 2.0);

        Assert.Equal(frame, at1);
        Assert.Equal(new Size(1600, 1200), at2);
    }

    [Fact]
    public void DisplaySize_ZoomIn_ScalesBothAxes()
    {
        var display = ZoomHelper.DisplaySize(new Size(100, 50), 2.0);
        Assert.Equal(new Size(200, 100), display);
    }

    [Fact]
    public void DisplaySize_ZoomOut_ScalesBothAxes()
    {
        var display = ZoomHelper.DisplaySize(new Size(100, 50), 0.5);
        Assert.Equal(new Size(50, 25), display);
    }

    [Fact]
    public void ClampZoom_EnforcesMinMax()
    {
        Assert.Equal(ZoomHelper.MinZoom, ZoomHelper.ClampZoom(0.001));
        Assert.Equal(ZoomHelper.MaxZoom, ZoomHelper.ClampZoom(100));
        Assert.Equal(1.0, ZoomHelper.ClampZoom(1.0));
    }

    [Fact]
    public void ZoomBySteps_InAndOut_AreInversesApproximately()
    {
        var z = 1.0;
        z = ZoomHelper.ZoomBySteps(z, 3);
        Assert.True(z > 1.0);
        z = ZoomHelper.ZoomBySteps(z, -3);
        Assert.InRange(z, 0.99, 1.01);
    }

    [Fact]
    public void FreeResizeSize_AllowsNonUniformStretch()
    {
        // Edge resize: width and height independent
        var free = ZoomHelper.FreeResizeSize(new Size(300, 100), minPx: 32);
        Assert.Equal(300, free.Width);
        Assert.Equal(100, free.Height);
        Assert.NotEqual(1.0, free.Width / (double)free.Height, 2);
    }

    [Fact]
    public void AspectPreserveSize_CornerResize_KeepsReferenceAspect()
    {
        var reference = new Size(200, 100); // 2:1
        var tentative = new Size(300, 180); // user dragged freely
        var result = ZoomHelper.AspectPreserveSize(reference, tentative);

        var refAspect = reference.Width / (double)reference.Height;
        var resultAspect = result.Width / (double)result.Height;
        Assert.InRange(resultAspect, refAspect - 0.02, refAspect + 0.02);
    }

    [Fact]
    public void ClampPan_WhenZoomedIn_KeepsContentCoveringViewport()
    {
        var viewport = new Size(100, 100);
        var content = new Size(300, 300);
        var pan = ZoomHelper.ClampPan(new Point(-500, -500), content, viewport);

        Assert.InRange(pan.X, viewport.Width - content.Width, 0);
        Assert.InRange(pan.Y, viewport.Height - content.Height, 0);
    }

    [Fact]
    public void ClampPan_WhenZoomedOut_CentersContent()
    {
        var viewport = new Size(200, 200);
        var content = new Size(100, 100);
        var pan = ZoomHelper.ClampPan(new Point(0, 0), content, viewport);

        Assert.Equal(50, pan.X);
        Assert.Equal(50, pan.Y);
    }

    [Fact]
    public void PanAfterZoom_KeepsPointUnderCursorStable()
    {
        var frame = new Size(200, 200);
        var oldZoom = 1.0;
        var newZoom = 2.0;
        var oldPan = new Point(0, 0);
        var cursor = new Point(50, 50);

        var newPan = ZoomHelper.PanAfterZoom(oldZoom, newZoom, oldPan, cursor, frame);

        // Content point under cursor at old zoom: (50,50) in content-at-zoom1 space
        // At zoom 2: content coord * 2 + pan = cursor => pan = cursor - content*2 = 50-100 = -50
        Assert.Equal(-50, newPan.X);
        Assert.Equal(-50, newPan.Y);
    }

    [Fact]
    public void FormatZoomPercent_ShowsHundredAtNative()
    {
        Assert.Equal("100%", ZoomHelper.FormatZoomPercent(1.0));
        Assert.Equal("200%", ZoomHelper.FormatZoomPercent(2.0));
        Assert.Equal("50%", ZoomHelper.FormatZoomPercent(0.5));
    }

    [Fact]
    public void IsCornerEdge_OnlyCorners()
    {
        Assert.True(ZoomHelper.IsCornerEdge("SE"));
        Assert.True(ZoomHelper.IsCornerEdge("NW"));
        Assert.False(ZoomHelper.IsCornerEdge("N"));
        Assert.False(ZoomHelper.IsCornerEdge("E"));
    }
}
