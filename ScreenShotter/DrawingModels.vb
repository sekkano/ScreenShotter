''' <summary>
''' Drawing tools available on the annotation toolbar.
''' </summary>
Public Enum DrawingTool
    ''' <summary>Move / resize / pan screenshots; select annotations.</summary>
    Pointer = 0
    ''' <summary>Semi-transparent freehand highlight on a screenshot.</summary>
    Highlighter = 1
    ''' <summary>Freehand pen (uses opacity from settings; often more opaque).</summary>
    Pen = 2
    ''' <summary>Drag out a border rectangle (editable afterward).</summary>
    Rectangle = 3
    ''' <summary>Drag an arrow from start to end (editable afterward).</summary>
    Arrow = 4
    ''' <summary>Click to place text (editable afterward).</summary>
    Text = 5
    ''' <summary>Freehand blur brush (privacy / redact), similar to highlighter.</summary>
    Blur = 6
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
''' Saved color / opacity / thickness for one ink tool.
''' </summary>
Public Class ToolAppearance
    Public Sub New(baseColor As Color, opacityPercent As Integer, thickness As Single)
        Me.BaseColor = Color.FromArgb(255, baseColor)
        Me.OpacityPercent = DrawingHelper.ClampOpacityPercent(opacityPercent)
        Me.Thickness = DrawingHelper.ClampThickness(thickness)
    End Sub

    Public Property BaseColor As Color
    Public Property OpacityPercent As Integer
    Public Property Thickness As Single

    Public Shared Function FromPreset(preset As DrawingToolPreset) As ToolAppearance
        Return New ToolAppearance(preset.BaseColor, preset.OpacityPercent, preset.Thickness)
    End Function
End Class

''' <summary>
''' Live ink settings from the drawing toolbar. Appearance is stored per ink tool
''' so switching Highlighter ↔ Pen restores each tool's last color/opacity/size.
''' </summary>
Public Class DrawingSettings
    Private _tool As DrawingTool = DrawingTool.Highlighter
    Private ReadOnly _appearance As New Dictionary(Of DrawingTool, ToolAppearance)()

    Public Sub New()
        ' Seed each draw tool with its defaults once; later edits are kept.
        EnsureSlot(DrawingTool.Highlighter)
        EnsureSlot(DrawingTool.Pen)
        EnsureSlot(DrawingTool.Blur)
        EnsureSlot(DrawingTool.Rectangle)
        EnsureSlot(DrawingTool.Arrow)
        EnsureSlot(DrawingTool.Text)
    End Sub

    Public Property Tool As DrawingTool
        Get
            Return _tool
        End Get
        Set(value As DrawingTool)
            SelectTool(value)
        End Set
    End Property

    ''' <summary>
    ''' Switches the active draw tool without resetting its saved appearance.
    ''' </summary>
    Public Sub SelectTool(drawTool As DrawingTool)
        Dim t = NormalizeDrawTool(drawTool)
        EnsureSlot(t)
        _tool = t
    End Sub

    ''' <summary>RGB base color for the active tool (alpha ignored; use OpacityPercent).</summary>
    Public Property BaseColor As Color
        Get
            Return CurrentAppearance().BaseColor
        End Get
        Set(value As Color)
            CurrentAppearance().BaseColor = Color.FromArgb(255, value)
        End Set
    End Property

    ''' <summary>0–100% ink opacity for the active tool.</summary>
    Public Property OpacityPercent As Integer
        Get
            Return CurrentAppearance().OpacityPercent
        End Get
        Set(value As Integer)
            CurrentAppearance().OpacityPercent = DrawingHelper.ClampOpacityPercent(value)
        End Set
    End Property

    ''' <summary>Stroke width in natural image pixels for the active tool.</summary>
    Public Property Thickness As Single
        Get
            Return CurrentAppearance().Thickness
        End Get
        Set(value As Single)
            CurrentAppearance().Thickness = DrawingHelper.ClampThickness(value)
        End Set
    End Property

    Public ReadOnly Property StrokeColor As Color
        Get
            Dim app = CurrentAppearance()
            Dim a = CInt(Math.Round(app.OpacityPercent / 100.0 * 255.0))
            a = Math.Max(0, Math.Min(255, a))
            Return Color.FromArgb(a, app.BaseColor)
        End Get
    End Property

    Public Function CreateStroke() As InkStroke
        Dim tool = NormalizeDrawTool(_tool)
        If Not DrawingHelper.IsInkTool(tool) Then tool = DrawingTool.Highlighter
        Dim app = CurrentAppearance()
        Dim a = CInt(Math.Round(app.OpacityPercent / 100.0 * 255.0))
        a = Math.Max(0, Math.Min(255, a))
        Dim inkColor = Color.FromArgb(a, app.BaseColor)
        Return New InkStroke(tool, inkColor, app.Thickness)
    End Function

    ''' <summary>Creates a rectangle annotation from current appearance.</summary>
    Public Function CreateRectAnnotation(bounds As RectangleF) As RectAnnotation
        Dim app = AppearanceFor(DrawingTool.Rectangle)
        Return New RectAnnotation() With {
            .Color = Color.FromArgb(255, app.BaseColor),
            .NativeSize = app.Thickness,
            .Bounds = AnnotationHelper.NormalizeRect(bounds)
        }
    End Function

    ''' <summary>Creates an arrow annotation from current appearance.</summary>
    Public Function CreateArrowAnnotation(startPt As PointF, endPt As PointF) As ArrowAnnotation
        Dim app = AppearanceFor(DrawingTool.Arrow)
        Return New ArrowAnnotation() With {
            .Color = Color.FromArgb(255, app.BaseColor),
            .NativeSize = app.Thickness,
            .Start = startPt,
            .End = endPt
        }
    End Function

    ''' <summary>Creates a text annotation from current appearance.</summary>
    Public Function CreateTextAnnotation(location As PointF, text As String) As TextAnnotation
        Dim app = AppearanceFor(DrawingTool.Text)
        Return New TextAnnotation() With {
            .Color = Color.FromArgb(255, app.BaseColor),
            .NativeSize = AnnotationHelper.ClampFontSize(app.Thickness),
            .Location = location,
            .Text = If(text, "")
        }
    End Function

    ''' <summary>
    ''' Resets one tool (or the active tool) back to factory defaults.
    ''' </summary>
    Public Sub ApplyToolPreset(drawTool As DrawingTool)
        Dim t = NormalizeDrawTool(drawTool)
        Dim preset = DrawingHelper.GetToolPreset(t)
        _appearance(t) = ToolAppearance.FromPreset(preset)
        _tool = t
    End Sub

    ''' <summary>
    ''' Appearance snapshot for a tool (for tests / UI sync).
    ''' </summary>
    Public Function GetAppearance(drawTool As DrawingTool) As ToolAppearance
        Return AppearanceFor(drawTool)
    End Function

    Private Function AppearanceFor(drawTool As DrawingTool) As ToolAppearance
        Dim t = NormalizeDrawTool(drawTool)
        EnsureSlot(t)
        Return _appearance(t)
    End Function

    Private Function CurrentAppearance() As ToolAppearance
        EnsureSlot(_tool)
        Return _appearance(_tool)
    End Function

    Private Sub EnsureSlot(drawTool As DrawingTool)
        Dim t = NormalizeDrawTool(drawTool)
        If Not _appearance.ContainsKey(t) Then
            _appearance(t) = ToolAppearance.FromPreset(DrawingHelper.GetToolPreset(t))
        End If
    End Sub

    Private Shared Function NormalizeDrawTool(tool As DrawingTool) As DrawingTool
        Select Case tool
            Case DrawingTool.Pen, DrawingTool.Highlighter, DrawingTool.Blur,
                 DrawingTool.Rectangle, DrawingTool.Arrow, DrawingTool.Text
                Return tool
            Case Else
                Return DrawingTool.Highlighter
        End Select
    End Function
End Class

''' <summary>
''' Default appearance for an ink tool.
''' </summary>
Public Structure DrawingToolPreset
    Private ReadOnly _tool As DrawingTool
    Private ReadOnly _baseColor As Color
    Private ReadOnly _opacityPercent As Integer
    Private ReadOnly _thickness As Single

    Public Sub New(tool As DrawingTool, baseColor As Color, opacityPercent As Integer, thickness As Single)
        _tool = tool
        _baseColor = baseColor
        _opacityPercent = opacityPercent
        _thickness = thickness
    End Sub

    Public ReadOnly Property Tool As DrawingTool
        Get
            Return _tool
        End Get
    End Property

    Public ReadOnly Property BaseColor As Color
        Get
            Return _baseColor
        End Get
    End Property

    Public ReadOnly Property OpacityPercent As Integer
        Get
            Return _opacityPercent
        End Get
    End Property

    Public ReadOnly Property Thickness As Single
        Get
            Return _thickness
        End Get
    End Property
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
        Return tool = DrawingTool.Highlighter OrElse
            tool = DrawingTool.Pen OrElse
            tool = DrawingTool.Blur
    End Function

    Public Function IsShapeTool(tool As DrawingTool) As Boolean
        Return tool = DrawingTool.Rectangle OrElse
            tool = DrawingTool.Arrow OrElse
            tool = DrawingTool.Text
    End Function

    ''' <summary>Any tool that draws onto a screenshot (not Pointer).</summary>
    Public Function IsAnnotationTool(tool As DrawingTool) As Boolean
        Return IsInkTool(tool) OrElse IsShapeTool(tool)
    End Function

    Public ReadOnly Property DefaultShapeBaseColor As Color
        Get
            Return Color.FromArgb(255, 220, 40, 40)
        End Get
    End Property

    Public ReadOnly Property DefaultTextBaseColor As Color
        Get
            Return Color.FromArgb(255, 20, 20, 20)
        End Get
    End Property

    Public Const DefaultShapeThickness As Single = 4.0F
    Public Const DefaultTextSize As Single = 28.0F
    Public Const DefaultBlurThickness As Single = 36.0F

    ''' <summary>
    ''' Distinct defaults per tool.
    ''' </summary>
    Public Function GetToolPreset(tool As DrawingTool) As DrawingToolPreset
        Select Case tool
            Case DrawingTool.Pen
                Return New DrawingToolPreset(
                    DrawingTool.Pen,
                    DefaultPenBaseColor,
                    DefaultPenOpacityPercent,
                    DefaultPenThickness)
            Case DrawingTool.Blur
                ' Color unused (blur samples the image); thickness = brush size
                Return New DrawingToolPreset(
                    DrawingTool.Blur,
                    Color.Gray,
                    100,
                    DefaultBlurThickness)
            Case DrawingTool.Rectangle
                Return New DrawingToolPreset(
                    DrawingTool.Rectangle,
                    DefaultShapeBaseColor,
                    100,
                    DefaultShapeThickness)
            Case DrawingTool.Arrow
                Return New DrawingToolPreset(
                    DrawingTool.Arrow,
                    DefaultShapeBaseColor,
                    100,
                    DefaultShapeThickness)
            Case DrawingTool.Text
                Return New DrawingToolPreset(
                    DrawingTool.Text,
                    DefaultTextBaseColor,
                    100,
                    DefaultTextSize)
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
            Case DrawingTool.Blur : Return "Blur"
            Case DrawingTool.Rectangle : Return "Rectangle"
            Case DrawingTool.Arrow : Return "Arrow"
            Case DrawingTool.Text : Return "Text"
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

    ''' <summary>
    ''' Locks a sample to a horizontal line through the stroke origin (Shift+draw).
    ''' </summary>
    Public Function ConstrainHorizontal(sample As PointF, originY As Single) As PointF
        Return New PointF(sample.X, originY)
    End Function
End Module
