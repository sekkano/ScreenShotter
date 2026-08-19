using System.Reflection;
using Xunit;

namespace ScreenShotter.Tests;

/// <summary>
/// Static structure checks against real shipped types (no form show / no screen grab).
/// </summary>
public class StructuralTypeTests
{
    [Fact]
    public void ShippedTypes_ExposeExpectedPublicSurface()
    {
        // Capture overlay (per-monitor) + session
        var overlay = typeof(CaptureOverlayForm);
        Assert.True(overlay.IsSubclassOf(typeof(Form)));
        Assert.NotNull(overlay.GetProperty("MonitorBounds"));
        Assert.NotNull(overlay.GetProperty("MonitorBitmap"));
        Assert.NotNull(overlay.GetMethod("InvalidateVirtualRegions"));

        Assert.NotNull(typeof(CaptureSession).GetMethod("Run"));
        Assert.NotNull(typeof(CaptureSession).GetMethod("TakeResult"));
        Assert.NotNull(typeof(CaptureSession).GetProperty("SelectionVirtual"));

        // Multi-monitor geometry
        Assert.NotNull(typeof(MonitorCaptureHelper).GetMethod(
            "IntersectVirtual",
            new[] { typeof(Rectangle), typeof(Rectangle) }));
        Assert.NotNull(typeof(MonitorCaptureHelper).GetMethod(
            "UnionMonitorBounds",
            new[] { typeof(IEnumerable<Rectangle>) }));

        // Per-tab canvas + movable/resizable/zoomable image at native size
        Assert.True(typeof(ScreenshotCanvas).IsSubclassOf(typeof(Panel)));
        Assert.NotNull(typeof(ScreenshotCanvas).GetMethod("AddScreenshotImage"));
        Assert.NotNull(typeof(ScreenshotCanvas).GetMethod("ZoomSelectedIn"));
        Assert.NotNull(typeof(ScreenshotCanvas).GetMethod("ZoomSelectedOut"));
        Assert.NotNull(typeof(ScreenshotCanvas).GetMethod("ZoomSelectedReset"));
        Assert.NotNull(typeof(ScreenshotCanvas).GetMethod("RemoveSelectedScreenshot"));
        Assert.NotNull(typeof(ScreenshotCanvas).GetMethod(
            "RemoveScreenshot",
            new[] { typeof(Guid), typeof(bool) }));
        Assert.NotNull(typeof(ScreenshotCanvas).GetMethod("Undo", Type.EmptyTypes));
        Assert.NotNull(typeof(ScreenshotCanvas).GetMethod("Redo", Type.EmptyTypes));
        Assert.NotNull(typeof(ScreenshotCanvas).GetMethod("CanUndo", Type.EmptyTypes));
        Assert.NotNull(typeof(ScreenshotCanvas).GetMethod("CanRedo", Type.EmptyTypes));
        Assert.NotNull(typeof(ScreenshotCanvas).GetMethod("RenderTabComposite"));
        Assert.NotNull(typeof(ScreenshotCanvas).GetMethod("SaveTabComposite", new[] { typeof(string) }));
        Assert.NotNull(typeof(ScreenshotCanvas).GetProperty("ScreenshotCount"));
        Assert.NotNull(typeof(UndoRedoStack).GetMethod("Push"));
        Assert.NotNull(typeof(UndoRedoStack).GetMethod("Undo"));
        Assert.NotNull(typeof(UndoRedoStack).GetMethod("Redo"));
        Assert.NotNull(typeof(TabSession).GetMethod("RemoveScreenshot", new[] { typeof(Guid) }));
        Assert.NotNull(typeof(TabExportHelper).GetMethod("ComputeUnionBounds"));
        Assert.NotNull(typeof(TabExportHelper).GetMethod("FormatFromPath", new[] { typeof(string) }));
        Assert.NotNull(typeof(TabExportHelper).GetMethod("CreateClipboardBitmap", new[] { typeof(Image) }));
        Assert.NotNull(typeof(MovableScreenshotBox).GetMethod("DrawContentAsDisplayed", new[] { typeof(Graphics), typeof(Rectangle) }));
        Assert.True(typeof(MovableScreenshotBox).IsSubclassOf(typeof(Control)));
        Assert.NotNull(typeof(MovableScreenshotBox).GetEvent("PositionChanged"));
        Assert.NotNull(typeof(MovableScreenshotBox).GetEvent("TransformChanged"));
        Assert.NotNull(typeof(MovableScreenshotBox).GetEvent("InteractionEnded"));
        Assert.NotNull(typeof(MovableScreenshotBox).GetProperty("Zoom"));
        Assert.NotNull(typeof(MovableScreenshotBox).GetProperty("NaturalSize"));
        Assert.NotNull(typeof(MovableScreenshotBox).GetProperty("IsInteracting"));
        Assert.NotNull(typeof(MovableScreenshotBox).GetMethod("ZoomIn"));
        Assert.NotNull(typeof(MovableScreenshotBox).GetMethod("ZoomOut"));
        Assert.NotNull(typeof(MovableScreenshotBox).GetMethod("ZoomReset"));

        // Zoom math module — frame-fixed content zoom + free/aspect resize
        Assert.NotNull(typeof(ZoomHelper).GetMethod("DisplaySize", new[] { typeof(Size), typeof(double) }));
        Assert.NotNull(typeof(ZoomHelper).GetMethod("ContentSize", new[] { typeof(Size), typeof(double) }));
        Assert.NotNull(typeof(ZoomHelper).GetMethod("AspectPreserveSize", new[] { typeof(Size), typeof(Size) }));
        Assert.NotNull(typeof(ZoomHelper).GetMethod("FreeResizeSize", new[] { typeof(Size), typeof(int) }));
        Assert.Equal(1.0, ZoomHelper.DefaultZoom);

        // Wheel routing helpers (image zoom vs canvas scroll)
        Assert.NotNull(typeof(WheelScrollHelper).GetMethod("DeltaFromWParam", new[] { typeof(IntPtr) }));
        Assert.NotNull(typeof(WheelScrollHelper).GetMethod("NextScrollPosition", new[] { typeof(Point), typeof(int), typeof(int) }));
        Assert.NotNull(typeof(WheelScrollHelper).GetMethod("IsScreenshotImageControl", new[] { typeof(Control) }));

        // First-click activate without eating the mouse message
        Assert.NotNull(typeof(MouseActivateHelper).GetMethod("WithoutEatingClick", new[] { typeof(int) }));
        Assert.True(typeof(ClickThroughToolStrip).IsSubclassOf(typeof(ToolStrip)));

        // Main form identity
        Assert.Equal("frmScreenShotter", typeof(frmScreenShotter).Name);
        Assert.True(typeof(frmScreenShotter).IsSubclassOf(typeof(Form)));
    }

    [Fact]
    public void RegionHelper_IsPublicModuleWithNormalizeRect()
    {
        // VB Module compiles to abstract sealed class with static methods
        var t = typeof(RegionHelper);
        var m = t.GetMethod("NormalizeRect", new[] { typeof(int), typeof(int), typeof(int), typeof(int) });
        Assert.NotNull(m);
        Assert.True(m!.IsStatic);
    }

    [Fact]
    public void WorkspaceModel_AndTabSession_AreIndependentCollections()
    {
        Assert.NotNull(typeof(WorkspaceModel).GetMethod("AddTab"));
        Assert.NotNull(typeof(TabSession).GetMethod("AddScreenshot"));
        Assert.NotNull(typeof(TabSession).GetMethod("MoveScreenshot"));
    }
}
