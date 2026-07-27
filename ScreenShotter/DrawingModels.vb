''' <summary>
''' Drawing tools available on the annotation toolbar.
''' </summary>
Public Enum DrawingTool
    ''' <summary>Move / resize / pan screenshots (no ink).</summary>
    Pointer = 0
    ''' <summary>Semi-transparent freehand highlight on a screenshot.</summary>
    Highlighter = 1
End Enum

''' <summary>
''' One freehand stroke stored in normalized image coordinates (0–1 across the natural image).
''' Survives zoom, pan, and frame resize.
''' </summary>
Public Class InkStroke
    Public Sub New(tool As DrawingTool, inkColor As Color, nativeWidth As Single)
        Me.Tool = tool
        Me.Color = inkColor
        Me.NativeWidth = Math.Max(1.0F, nativeWidth)
        Points = New List(Of PointF)()
    End Sub

    Public ReadOnly Property Tool As DrawingTool
    Public Property Color As Color
    ''' <summary>Stroke width in natural image pixels (scaled when drawn).</summary>
    Public Property NativeWidth As Single
    Public ReadOnly Property Points As List(Of PointF)

    Public ReadOnly Property HasSegment As Boolean
        Get
            Return Points.Count >= 2
        End Get
    End Property
End Class

''' <summary>
''' Pure helpers for highlighter defaults and image-normalized stroke coordinates.
''' </summary>
Public Module DrawingHelper
    Public ReadOnly Property HighlighterColor As Color
        Get
            ' Translucent yellow/gold
            Return Color.FromArgb(110, 255, 230, 0)
        End Get
    End Property

    Public Const HighlighterNativeWidth As Single = 28.0F

    Public Function CreateHighlighterStroke() As InkStroke
        Return New InkStroke(DrawingTool.Highlighter, HighlighterColor, HighlighterNativeWidth)
    End Function

    ''' <summary>
    ''' Maps a point in the screenshot viewport to normalized image coords (0–1), or Nothing if outside the image.
    ''' </summary>
    Public Function ViewportToNormalized(
        local As Point,
        frameSize As Size,
        pan As Point,
        zoom As Double) As PointF?

        Dim content = ZoomHelper.ContentSize(frameSize, zoom)
        If content.Width <= 0 OrElse content.Height <= 0 Then Return Nothing

        Dim x = local.X - pan.X
        Dim y = local.Y - pan.Y
        If x < 0 OrElse y < 0 OrElse x > content.Width OrElse y > content.Height Then
            Return Nothing
        End If

        Return New PointF(
            CSng(x / CDbl(content.Width)),
            CSng(y / CDbl(content.Height)))
    End Function

    ''' <summary>
    ''' Maps normalized image coords to a viewport point (may lie outside the visible frame when panned).
    ''' </summary>
    Public Function NormalizedToViewport(
        norm As PointF,
        frameSize As Size,
        pan As Point,
        zoom As Double) As PointF

        Dim content = ZoomHelper.ContentSize(frameSize, zoom)
        Return New PointF(
            pan.X + norm.X * content.Width,
            pan.Y + norm.Y * content.Height)
    End Function

    ''' <summary>
    ''' Stroke width in viewport pixels for the current zoom/frame.
    ''' </summary>
    Public Function ViewportStrokeWidth(
        nativeWidth As Single,
        frameSize As Size,
        naturalSize As Size,
        zoom As Double) As Single

        If naturalSize.Width <= 0 Then Return nativeWidth
        Dim content = ZoomHelper.ContentSize(frameSize, zoom)
        Dim scale = content.Width / CSng(naturalSize.Width)
        Return Math.Max(1.0F, nativeWidth * scale)
    End Function

    ''' <summary>
    ''' Clamps a normalized point into 0–1.
    ''' </summary>
    Public Function ClampNormalized(p As PointF) As PointF
        Return New PointF(
            Math.Max(0.0F, Math.Min(1.0F, p.X)),
            Math.Max(0.0F, Math.Min(1.0F, p.Y)))
    End Function
End Module
