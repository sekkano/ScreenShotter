using System.Drawing;
using Xunit;

namespace ScreenShotter.Tests;

/// <summary>
/// Exercises real WheelScrollHelper used for canvas scroll vs image zoom routing.
/// </summary>
public class WheelScrollHelperTests
{
    [Fact]
    public void DeltaFromWParam_PositiveAndNegative()
    {
        // HIWORD = +120
        var pos = new IntPtr(120 << 16);
        Assert.Equal(120, WheelScrollHelper.DeltaFromWParam(pos));

        // HIWORD = -120 → 0xFF880000 in low 32 bits
        unchecked
        {
            uint packed = (uint)((short)-120 << 16);
            var neg = new IntPtr(packed);
            Assert.Equal(-120, WheelScrollHelper.DeltaFromWParam(neg));
        }
    }

    [Fact]
    public void DeltaToScrollPixels_ScalesByLineHeight()
    {
        Assert.Equal(48, WheelScrollHelper.DeltaToScrollPixels(120, linePixels: 48));
        Assert.Equal(-48, WheelScrollHelper.DeltaToScrollPixels(-120, linePixels: 48));
        Assert.Equal(0, WheelScrollHelper.DeltaToScrollPixels(0));
    }

    [Fact]
    public void NextScrollPosition_FromNegativeGetter_AddsDeltas()
    {
        // AutoScrollPosition getter often returns (-x, -y)
        var current = new Point(-100, -50);
        var next = WheelScrollHelper.NextScrollPosition(current, deltaXPixels: 30, deltaYPixels: 10);

        Assert.Equal(130, next.X);
        Assert.Equal(60, next.Y);
    }

    [Fact]
    public void NextScrollPosition_ClampsBelowZero()
    {
        var current = new Point(0, 0);
        var next = WheelScrollHelper.NextScrollPosition(current, deltaXPixels: -40, deltaYPixels: -10);
        Assert.Equal(0, next.X);
        Assert.Equal(0, next.Y);
    }

    [Fact]
    public void IsScreenshotImageControl_Null_IsFalse()
    {
        Assert.False(WheelScrollHelper.IsScreenshotImageControl(null!));
    }
}
