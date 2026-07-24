using System.Drawing;
using Xunit;

namespace ScreenShotter.Tests;

/// <summary>
/// Exercises real TabSession / WorkspaceModel isolation and move logic.
/// </summary>
public class WorkspaceModelTests
{
    [Fact]
    public void AddScreenshot_ToTabA_DoesNotAppearOnTabB()
    {
        var workspace = new WorkspaceModel();
        var tabA = workspace.AddTab("A");
        var tabB = workspace.AddTab("B");

        var item = tabA.AddScreenshot(new Point(10, 20), new Size(100, 50));

        Assert.Single(tabA.Items);
        Assert.Empty(tabB.Items);
        Assert.Equal(item.Id, tabA.Items[0].Id);
        Assert.DoesNotContain(tabB.Items, i => i.Id == item.Id);
    }

    [Fact]
    public void MoveScreenshot_UpdatesOnlyThatItemPosition()
    {
        var tab = new TabSession("T");
        var first = tab.AddScreenshot(new Point(0, 0), new Size(10, 10));
        var second = tab.AddScreenshot(new Point(50, 50), new Size(20, 20));

        var moved = tab.MoveScreenshot(first.Id, new Point(33, 44));

        Assert.True(moved);
        Assert.Equal(new Point(33, 44), first.Location);
        Assert.Equal(new Point(50, 50), second.Location);
    }

    [Fact]
    public void MoveScreenshot_UnknownId_ReturnsFalseAndLeavesItemsUnchanged()
    {
        var tab = new TabSession("T");
        var item = tab.AddScreenshot(new Point(1, 2), new Size(3, 4));
        var original = item.Location;

        var moved = tab.MoveScreenshot(Guid.NewGuid(), new Point(9, 9));

        Assert.False(moved);
        Assert.Equal(original, item.Location);
    }

    [Fact]
    public void Workspace_ActiveTab_TracksLastAddedAndIndexChanges()
    {
        var workspace = new WorkspaceModel();
        Assert.Null(workspace.ActiveTab);

        var t1 = workspace.AddTab("One");
        Assert.Same(t1, workspace.ActiveTab);
        Assert.Equal(0, workspace.ActiveTabIndex);

        var t2 = workspace.AddTab("Two");
        Assert.Same(t2, workspace.ActiveTab);
        Assert.Equal(1, workspace.ActiveTabIndex);

        workspace.ActiveTabIndex = 0;
        Assert.Same(t1, workspace.ActiveTab);
    }

    [Fact]
    public void RemoveTabAt_IsolatesRemainingTabScreenshots()
    {
        var workspace = new WorkspaceModel();
        var tabA = workspace.AddTab("A");
        var tabB = workspace.AddTab("B");
        tabA.AddScreenshot(new Point(1, 1), new Size(5, 5));
        tabB.AddScreenshot(new Point(2, 2), new Size(6, 6));

        Assert.True(workspace.RemoveTabAt(0));
        Assert.Single(workspace.Tabs);
        Assert.Same(tabB, workspace.Tabs[0]);
        Assert.Single(workspace.Tabs[0].Items);
        Assert.Equal(new Point(2, 2), workspace.Tabs[0].Items[0].Location);
    }

    [Fact]
    public void MultipleScreenshotsOnSameTab_CanOverlapPositions()
    {
        var tab = new TabSession("Overlap");
        var a = tab.AddScreenshot(new Point(10, 10), new Size(100, 100));
        var b = tab.AddScreenshot(new Point(30, 30), new Size(100, 100));

        // Same tab holds both; positions freely set (overlap is allowed)
        Assert.Equal(2, tab.Items.Count);
        Assert.True(a.Location.X < b.Location.X + b.Size.Width);
        Assert.True(a.Location.Y < b.Location.Y + b.Size.Height);
        Assert.NotEqual(a.Id, b.Id);
    }

    [Fact]
    public void RemoveScreenshot_DeletesOnlyThatItem()
    {
        var tab = new TabSession("T");
        var keep = tab.AddScreenshot(new Point(0, 0), new Size(10, 10));
        var drop = tab.AddScreenshot(new Point(5, 5), new Size(20, 20));

        Assert.True(tab.RemoveScreenshot(drop.Id));
        Assert.Single(tab.Items);
        Assert.Equal(keep.Id, tab.Items[0].Id);
        Assert.False(tab.RemoveScreenshot(drop.Id)); // already gone
        Assert.False(tab.RemoveScreenshot(Guid.NewGuid()));
    }

    [Fact]
    public void AddTab_DefaultName_UsesCurrentCount_NotLifetimeCounter()
    {
        var workspace = new WorkspaceModel();
        Assert.Equal("Tab 1", workspace.AddTab().Name);
        Assert.Equal("Tab 2", workspace.AddTab().Name);
        Assert.True(workspace.RemoveTabAt(1));
        // After closing Tab 2, next default name is Tab 2 again (count-based)
        Assert.Equal("Tab 2", workspace.AddTab().Name);
    }

    [Fact]
    public void RenameTabAt_UpdatesName()
    {
        var workspace = new WorkspaceModel();
        workspace.AddTab();
        Assert.True(workspace.RenameTabAt(0, "  Snips  "));
        Assert.Equal("Snips", workspace.Tabs[0].Name);
        Assert.False(workspace.RenameTabAt(0, "   "));
        Assert.Equal("Snips", workspace.Tabs[0].Name);
    }
}
