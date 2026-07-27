''' <summary>
''' Drawing tools available on the annotation toolbar.
''' </summary>
Public Enum DrawingTool
    ''' <summary>Move / resize / pan screenshots (no ink).</summary>
    Pointer = 0
    ''' <summary>Semi-transparent freehand highlight on a screenshot.</summary>
    Highlighter = 1
    ''' <summary>Freehand pen (uses opacity from settings; often more opaque).</summary>
    Pen = 2
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
''' Live ink settings from the drawing toolbar (shared per tab via the canvas).
''' </summary>
Public Class DrawingSettings
    Private _opacityPercent As Integer = 43
    Private _thickness As Single = 28.0F
    Private _baseColor As Color = Color.FromArgb(255, 255, 230, 0)
    Private _tool As DrawingTool = DrawingTool.Highlighter

    Public Property Tool As DrawingTool
        Get
            Return _tool
        End Get
        Set(value As DrawingTool)
            If value = DrawingTool.Pointer Then
                _tool = DrawingTool.Highlighter
            Else
                _tool = value
            End If
        End Set
    End Property

    ''' <summary>RGB base color (alpha ignored; use OpacityPercent).</summary>
    Public Property BaseColor As Color
        Get
            Return Color.FromArgb(255, _baseColor)
        End Get
        Set(value As Color)
            _baseColor = Color.FromArgb(255, value)
        End Set
    End Property

    ''' <summary>0–100% ink opacity.</summary>
    Public Property OpacityPercent As Integer
        Get
            Return _opacityPercent
        End Get
        Set(value As Integer)
            _opacityPercent = DrawingHelper.ClampOpacityPercent(value)
        End Set
    End Property

    ''' <summary>Stroke width in natural image pixels.</summary>
    Public Property Thickness As Single
        Get
            Return _thickness
        End Get
        Set(value As Single)
            _thickness = DrawingHelper.ClampThickness(value)
        End Set
    End Property

    Public ReadOnly Property StrokeColor As Color
        Get
            Dim a = CInt(Math.Round(_opacityPercent / 100.0 * 255.0))
            a = Math.Max(0, Math.Min(255, a))
            Return Color.FromArgb(a, _baseColor)
        End Get
    End Property

    Public Function CreateStroke() As InkStroke
        Dim tool = If(_tool = DrawingTool.Pointer, DrawingTool.Highlighter, _tool)
        Return New InkStroke(tool, StrokeColor, _thickness)
    End Function

    ''' <summary>
    ''' Applies default color / opacity / thickness for the selected ink tool.
    ''' Highlighter = wide translucent yellow; Pen = thin opaque black.
    ''' </summary>
    Public Sub ApplyToolPreset(tool As DrawingTool)
        Dim preset = DrawingHelper.GetToolPreset(tool)
        Tool = preset.Tool
        BaseColor = preset.BaseColor
        OpacityPercent = preset.OpacityPercent
        Thickness = preset.Thickness
    End Sub
End Class

''' <summary>
''' Default appearance for an ink tool.
''' </summary>
Public Structure DrawingToolPreset
    Public Sub New(tool As DrawingTool, baseColor As Color, opacityPercent As Integer, thickness As Single)
        Me.Tool = tool
        Me.BaseColor = baseColor
        Me.OpacityPercent = opacityPercent
        Me.Thickness = thickness
    End Sub

    Public ReadOnly Property Tool As DrawingTool
    Public ReadOnly Property BaseColor As Color
    Public ReadOnly Property OpacityPercent As Integer
    Public ReadOnly Property Thickness As Single
End Structure

''' <summary>
''' Pure helpers for drawing defaults and image-normalized stroke coordinates.
''' </summary>
Public Module DrawingHelper
    Public ReadOnly Property DefaultHighlighterBaseColor As Color
        Get
            Return Color.FromArgb(255, 255, 230, 0)
        End Get
    End Property

    Public ReadOnly Property DefaultPenBaseColor As Color
        Get
            Return Color.FromArgb(255, 20, 20, 20)
        End Get
    End Property

    Public Const DefaultOpacityPercent As Integer = 43
    Public Const DefaultThickness As Single = 28.0F
    Public Const DefaultPenOpacityPercent As Integer = 100
    Public Const DefaultPenThickness As Single = 4.0F
    Public Const MinThickness As Single = 2.0F
    Public Const MaxThickness As Single = 96.0F

    Public Function IsInkTool(tool As DrawingTool) As Boolean
        Return tool = DrawingTool.Highlighter OrElse tool = DrawingTool.Pen
    End Function

    ''' <summary>
    ''' Distinct defaults: Highlighter is a wide soft translucent mark; Pen is thin solid ink.
    ''' </summary>
    Public Function GetToolPreset(tool As DrawingTool) As DrawingToolPreset
        Select Case tool
            Case DrawingTool.Pen
                Return New DrawingToolPreset(
                    DrawingTool.Pen,
                    DefaultPenBaseColor,
                    DefaultPenOpacityPercent,
                    DefaultPenThickness)
            Case Else
                Return New DrawingToolPreset(
                    DrawingTool.Highlighter,
                    DefaultHighlighterBaseColor,
                    DefaultOpacityPercent,
                    DefaultThickness)
        End Select
    End Function

    Public Function ClampOpacityPercent(value As Integer) As Integer
        Return Math.Max(0, Math.Min(100, value))
    End Function

    Public Function ClampThickness(value As Single) As Single
        If Single.IsNaN(value) OrElse Single.IsInfinity(value) Then Return DefaultThickness
        Return Math.Max(MinThickness, Math.Min(MaxThickness, value))
    End Function

    Public Function ColorWithOpacity(baseColor As Color, opacityPercent As Integer) As Color
        Dim pct = ClampOpacityPercent(opacityPercent)
        Dim a = CInt(Math.Round(pct / 100.0 * 255.0))
        Return Color.FromArgb(Math.Max(0, Math.Min(255, a)), baseColor)
    End Function

    Public Function ToolDisplayName(tool As DrawingTool) As String
        Select Case tool
            Case DrawingTool.Highlighter : Return "Highlighter"
            Case DrawingTool.Pen : Return "Pen"
            Case DrawingTool.Pointer : Return "Pointer"
            Case Else : Return tool.ToString()
        End Select
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
