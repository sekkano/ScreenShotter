using Xunit;

namespace ScreenShotter.Tests;

/// <summary>
/// Exercises real MouseActivateHelper — inactive window first-click pass-through.
/// </summary>
public class MouseActivateHelperTests
{
    [Fact]
    public void WithoutEatingClick_ConvertsActivateAndEat()
    {
        Assert.Equal(
            MouseActivateHelper.MA_ACTIVATE,
            MouseActivateHelper.WithoutEatingClick(MouseActivateHelper.MA_ACTIVATEANDEAT));
    }

    [Fact]
    public void WithoutEatingClick_ConvertsNoActivateAndEat()
    {
        Assert.Equal(
            MouseActivateHelper.MA_NOACTIVATE,
            MouseActivateHelper.WithoutEatingClick(MouseActivateHelper.MA_NOACTIVATEANDEAT));
    }

    [Fact]
    public void WithoutEatingClick_LeavesActivateUnchanged()
    {
        Assert.Equal(
            MouseActivateHelper.MA_ACTIVATE,
            MouseActivateHelper.WithoutEatingClick(MouseActivateHelper.MA_ACTIVATE));
    }

    [Fact]
    public void ActivateAndPassClick_IsMaActivate()
    {
        Assert.Equal(MouseActivateHelper.MA_ACTIVATE, MouseActivateHelper.ActivateAndPassClick.ToInt32());
    }
}
