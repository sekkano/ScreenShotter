using System.Drawing;
using Xunit;

namespace ScreenShotter.Tests;

public class AnnotationHelperTests
{
    [Fact]
    public void NormalizeRect_FlipsNegativeSize()
    {
        var r = AnnotationHelper.NormalizeRect(new RectangleF(0.5f, 0.4f, -0.2f, -0.1f));
        Assert.Equal(0.3f, r.X, 3);
        Assert.Equal(0.3f, r.Y, 3);
        Assert.Equal(0.2f, r.Width, 3);
        Assert.Equal(0.1f, r.Height, 3);
    }

    [Fact]
    public void RectFromCorners_AnyDragDirection()
    {
        var r = AnnotationHelper.RectFromCorners(new PointF(0.8f, 0.2f), new PointF(0.1f, 0.9f));
        Assert.Equal(0.1f, r.Left, 3);
        Assert.Equal(0.2f, r.Top, 3);
        Assert.Equal(0.8f, r.Right, 3);
        Assert.Equal(0.9f, r.Bottom, 3);
    }

    [Fact]
    public void DistanceToSegment_MidpointIsNearZero()
    {
        var a = new PointF(0, 0);
        var b = new PointF(1, 0);
        var mid = new PointF(0.5f, 0);
        Assert.True(AnnotationHelper.DistanceToSegment(mid, a, b) < 0.0001f);
        Assert.True(AnnotationHelper.DistanceToSegment(new PointF(0.5f, 0.1f), a, b) > 0.05f);
    }

    [Fact]
    public void ArrowHeadPoints_ReturnsThreeVertices()
    {
        var head = AnnotationHelper.ArrowHeadPoints(new PointF(0, 0), new PointF(100, 0), 4);
        Assert.Equal(3, head.Length);
        Assert.Equal(100f, head[0].X, 1);
    }

    [Fact]
    public void ClampFontSize_RespectsRange()
    {
        Assert.Equal(AnnotationHelper.MinFontSize, AnnotationHelper.ClampFontSize(1));
        Assert.Equal(AnnotationHelper.MaxFontSize, AnnotationHelper.ClampFontSize(999));
        Assert.Equal(24f, AnnotationHelper.ClampFontSize(24));
    }

    [Fact]
    public void RectAnnotation_HitTest_PrefersBorder()
    {
        var rect = new RectAnnotation
        {
            Bounds = new RectangleF(0.2f, 0.2f, 0.4f, 0.4f),
            Color = Color.Red,
            NativeSize = 4
        };
        Assert.True(rect.HitTest(new PointF(0.2f, 0.4f), 0.02f)); // left border
        Assert.False(rect.HitTest(new PointF(0.4f, 0.4f), 0.01f)); // deep interior
    }

    [Fact]
    public void IsShapeTool_And_IsAnnotationTool()
    {
        Assert.True(DrawingHelper.IsShapeTool(DrawingTool.Rectangle));
        Assert.True(DrawingHelper.IsShapeTool(DrawingTool.Arrow));
        Assert.True(DrawingHelper.IsShapeTool(DrawingTool.Text));
        Assert.False(DrawingHelper.IsShapeTool(DrawingTool.Pen));
        Assert.True(DrawingHelper.IsAnnotationTool(DrawingTool.Rectangle));
        Assert.True(DrawingHelper.IsAnnotationTool(DrawingTool.Pen));
        Assert.False(DrawingHelper.IsAnnotationTool(DrawingTool.Pointer));
    }

    [Fact]
    public void DrawingSettings_CreateShapeAnnotations()
    {
        var settings = new DrawingSettings();
        settings.SelectTool(DrawingTool.Rectangle);
        settings.BaseColor = Color.Blue;
        settings.Thickness = 8;
        var rect = settings.CreateRectAnnotation(new RectangleF(0.1f, 0.1f, 0.2f, 0.3f));
        Assert.Equal(8f, rect.NativeSize);
        Assert.Equal(255, rect.Color.A);

        settings.SelectTool(DrawingTool.Arrow);
        var arrow = settings.CreateArrowAnnotation(new PointF(0, 0), new PointF(1, 1));
        Assert.Equal(new PointF(1, 1), arrow.End);

        settings.SelectTool(DrawingTool.Text);
        settings.Thickness = 36;
        var text = settings.CreateTextAnnotation(new PointF(0.2f, 0.2f), "Hi");
        Assert.Equal("Hi", text.Text);
        Assert.Equal(36f, text.NativeSize);
    }
}
