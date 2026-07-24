using System.Drawing;
using Xunit;

namespace ScreenShotter.Tests;

/// <summary>
/// Exercises real MonitorCaptureHelper multi-monitor geometry (any monitor count).
/// </summary>
public class MonitorCaptureHelperTests
{
    private static readonly Rectangle Primary = new(0, 0, 1920, 1080);
    private static readonly Rectangle SecondaryRight = new(1920, 0, 1920, 1080);
    private static readonly Rectangle SecondaryLeft = new(-1680, 0, 1680, 1050);

    [Fact]
    public void ToVirtualAndLocal_RoundTrip()
    {
        var local = new Point(100, 200);
        var virt = MonitorCaptureHelper.ToVirtualPoint(SecondaryRight, local);
        Assert.Equal(new Point(2020, 200), virt);
        Assert.Equal(local, MonitorCaptureHelper.ToLocalPoint(SecondaryRight, virt));
    }

    [Fact]
    public void SelectionSpanningTwoMonitors_SplitsIntoPerMonitorSources()
    {
        // Selection from mid primary into secondary
        var selection = new Rectangle(1800, 100, 200, 50); // 1800..2000

        var onPrimary = MonitorCaptureHelper.IntersectVirtual(selection, Primary);
        var onSecondary = MonitorCaptureHelper.IntersectVirtual(selection, SecondaryRight);

        Assert.Equal(new Rectangle(1800, 100, 120, 50), onPrimary);
        Assert.Equal(new Rectangle(1920, 100, 80, 50), onSecondary);

        var srcPrimary = MonitorCaptureHelper.SourceInMonitorBitmap(Primary, onPrimary);
        var srcSecondary = MonitorCaptureHelper.SourceInMonitorBitmap(SecondaryRight, onSecondary);
        Assert.Equal(new Rectangle(1800, 100, 120, 50), srcPrimary);
        Assert.Equal(new Rectangle(0, 100, 80, 50), srcSecondary);

        var dstPrimary = MonitorCaptureHelper.DestinationInCapture(selection, onPrimary);
        var dstSecondary = MonitorCaptureHelper.DestinationInCapture(selection, onSecondary);
        Assert.Equal(new Rectangle(0, 0, 120, 50), dstPrimary);
        Assert.Equal(new Rectangle(120, 0, 80, 50), dstSecondary);
        Assert.Equal(selection.Width, dstPrimary.Width + dstSecondary.Width);
    }

    [Fact]
    public void SelectionOnLeftNegativeMonitor_MapsCorrectly()
    {
        var selection = new Rectangle(-100, 10, 50, 40);
        var inter = MonitorCaptureHelper.IntersectVirtual(selection, SecondaryLeft);
        Assert.Equal(selection, inter);

        var src = MonitorCaptureHelper.SourceInMonitorBitmap(SecondaryLeft, inter);
        Assert.Equal(new Rectangle(1580, 10, 50, 40), src);

        var local = MonitorCaptureHelper.ToLocalRect(SecondaryLeft, inter);
        Assert.Equal(src, local);
    }

    [Fact]
    public void UnionMonitorBounds_AnyCount()
    {
        var one = MonitorCaptureHelper.UnionMonitorBounds(new[] { Primary });
        Assert.Equal(Primary, one);

        var two = MonitorCaptureHelper.UnionMonitorBounds(new[] { Primary, SecondaryRight });
        Assert.Equal(new Rectangle(0, 0, 3840, 1080), two);

        var three = MonitorCaptureHelper.UnionMonitorBounds(new[] { SecondaryLeft, Primary, SecondaryRight });
        Assert.Equal(new Rectangle(-1680, 0, 1680 + 3840, 1080), three);
    }

    [Fact]
    public void SelectionTouchesMonitor_OnlyWhenIntersecting()
    {
        var sel = new Rectangle(1900, 0, 50, 50);
        Assert.True(MonitorCaptureHelper.SelectionTouchesMonitor(sel, Primary));
        Assert.True(MonitorCaptureHelper.SelectionTouchesMonitor(sel, SecondaryRight));
        Assert.False(MonitorCaptureHelper.SelectionTouchesMonitor(sel, SecondaryLeft));
    }

    [Fact]
    public void CrosshairDirtyRegions_CoverFullClientAxes()
    {
        var regions = MonitorCaptureHelper.CrosshairDirtyRegions(new Point(50, 80), new Size(200, 150), 3);
        Assert.Equal(2, regions.Length);
        Assert.Equal(200, regions[0].Width); // horizontal strip
        Assert.Equal(150, regions[1].Height); // vertical strip
    }

    [Fact]
    public void UnionDirty_MergesSelectionFrames()
    {
        var a = new Rectangle(10, 10, 20, 20);
        var b = new Rectangle(25, 25, 20, 20);
        var u = MonitorCaptureHelper.UnionDirty(a, b);
        Assert.Equal(Rectangle.Union(a, b), u);
    }
}
