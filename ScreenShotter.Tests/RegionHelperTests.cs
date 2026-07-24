using System.Drawing;
using Xunit;

namespace ScreenShotter.Tests;

/// <summary>
/// Exercises the real RegionHelper shipped in the app (not a re-implementation).
/// </summary>
public class RegionHelperTests
{
    [Fact]
    public void NormalizeRect_DragDownRight_ReturnsExpectedBounds()
    {
        var rect = RegionHelper.NormalizeRect(10, 20, 50, 80);

        Assert.Equal(10, rect.X);
        Assert.Equal(20, rect.Y);
        Assert.Equal(40, rect.Width);
        Assert.Equal(60, rect.Height);
    }

    [Fact]
    public void NormalizeRect_DragUpLeft_NormalizesToPositiveSize()
    {
        // Reverse drag: end is above and left of start
        var rect = RegionHelper.NormalizeRect(100, 100, 40, 30);

        Assert.Equal(40, rect.X);
        Assert.Equal(30, rect.Y);
        Assert.Equal(60, rect.Width);
        Assert.Equal(70, rect.Height);
    }

    [Fact]
    public void NormalizeRect_DragDownLeft_NormalizesCorrectly()
    {
        var rect = RegionHelper.NormalizeRect(200, 10, 50, 90);

        Assert.Equal(50, rect.X);
        Assert.Equal(10, rect.Y);
        Assert.Equal(150, rect.Width);
        Assert.Equal(80, rect.Height);
    }

    [Fact]
    public void NormalizeRect_PointOverload_MatchesCoordinateOverload()
    {
        var fromPoints = RegionHelper.NormalizeRect(new Point(5, 15), new Point(25, 5));
        var fromCoords = RegionHelper.NormalizeRect(5, 15, 25, 5);

        Assert.Equal(fromCoords, fromPoints);
    }

    [Fact]
    public void NormalizeRect_ZeroArea_IsEmptySize()
    {
        var rect = RegionHelper.NormalizeRect(10, 10, 10, 10);

        Assert.Equal(0, rect.Width);
        Assert.Equal(0, rect.Height);
        Assert.False(RegionHelper.IsValidCaptureRegion(rect));
    }

    [Fact]
    public void IsValidCaptureRegion_PositiveArea_ReturnsTrue()
    {
        var rect = RegionHelper.NormalizeRect(0, 0, 1, 1);
        Assert.True(RegionHelper.IsValidCaptureRegion(rect));
    }

    [Theory]
    [InlineData(0, 0, 0, 5)] // zero width
    [InlineData(0, 0, 5, 0)] // zero height
    public void IsValidCaptureRegion_ZeroDimension_ReturnsFalse(int x1, int y1, int x2, int y2)
    {
        var rect = RegionHelper.NormalizeRect(x1, y1, x2, y2);
        Assert.False(RegionHelper.IsValidCaptureRegion(rect));
    }
}
