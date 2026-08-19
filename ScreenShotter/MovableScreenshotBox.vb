''' <summary>
''' Movable / resizable screenshot viewport.
''' - Frame size changes only via edge/corner drag (not zoom)
''' - Edge resize: free stretch; corner resize: aspect preserved
''' - Zoom scales image inside the fixed frame (pan follows cursor)
''' - Single custom-painted control (no child PictureBox) to avoid move ghosting
''' </summary>
Partial Public Class MovableScreenshotBox
    Inherits Control

    Private Enum InteractMode
        None
        Move
        Resize
        Pan
        Draw
        Annotate
        AnnotEdit
    End Enum

    Private Enum AnnotEditKind
        None
        Move
        ResizeRect
        MoveArrowStart
        MoveArrowEnd
        ScaleText
    End Enum

    Private Enum ResizeEdge
        None
        N
        S
        E
        W
        NE
        NW
        SE
        SW
    End Enum

    Private ReadOnly _itemId As Guid
    Private ReadOnly _image As Image
    Private ReadOnly _naturalSize As Size
    Private ReadOnly _canvas As ScreenshotCanvas
    Private ReadOnly _strokes As New List(Of InkStroke)()
    Private _activeStroke As InkStroke
    Private ReadOnly _annotations As New List(Of AnnotationBase)()
    Private _selectedAnnotation As AnnotationBase
    Private _annotationDraft As AnnotationBase
    Private _annotateStartNorm As PointF
    Private _annotateTool As DrawingTool
    Private _annotEditKind As AnnotEditKind = AnnotEditKind.None
    Private _annotEditOriginal As AnnotationBase
    Private _annotEditGrabNorm As PointF
    Private _pendingTextClick As Boolean
    Private _pendingTextLocal As Point
    Private _zoom As Double = ZoomHelper.DefaultZoom
    Private _pan As Point = Point.Empty
    Private _mode As InteractMode = InteractMode.None
    Private _dragOffset As Point
    Private _resizeStartCursor As Point
    Private _resizeStartLocation As Point
    Private _resizeStartSize As Size
    Private _resizeEdge As ResizeEdge
    Private _panStartCursor As Point
    Private _panStart As Point
    Private _selected As Boolean
    Private _aspectReference As Size
    ''' <summary>When true, new samples keep the first stroke point's Y (Shift+draw horizontal).</summary>
    Private _drawLockHorizontal As Boolean
    ''' <summary>Click-vs-drag: wait for a few pixels of movement before entering Move.</summary>
    Private _pendingMove As Boolean
    Private _pendingMoveLocal As Point
    Private _pendingMoveScreen As Point
    Private _endingInteract As Boolean
    Private _transformAtInteractionStart As BoxTransformState
    Private _hasTransformBaseline As Boolean

    Private Const Grip As Integer = 10
    Private Const MinDisplayPx As Integer = 48
    ''' <summary>Pixels of mouse travel before a click becomes a move (avoids jump on select).</summary>
    Private Const MoveDragThresholdPx As Integer = 8

    Public ReadOnly Property ItemId As Guid
        Get
            Return _itemId
        End Get
    End Property

    Public ReadOnly Property NaturalSize As Size
        Get
            Return _naturalSize
        End Get
    End Property

    ''' <summary>
    ''' Source image for this screenshot (owned by the canvas; do not dispose).
    ''' </summary>
    Public ReadOnly Property SourceImage As Image
        Get
            Return _image
        End Get
    End Property

    ''' <summary>
    ''' Current pan of zoomed content inside the frame.
    ''' </summary>
    Public ReadOnly Property ContentPan As Point
        Get
            Return _pan
        End Get
    End Property

    <System.ComponentModel.Browsable(False)>
    <System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)>
    Public Property Zoom As Double
        Get
            Return _zoom
        End Get
        Set(value As Double)
            SetZoom(value, Point.Empty, useCursor:=False, notify:=True)
        End Set
    End Property

    <System.ComponentModel.Browsable(False)>
    <System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)>
    Public Property Selected As Boolean
        Get
            Return _selected
        End Get
        Set(value As Boolean)
            If _selected = value Then Return
            _selected = value
            Invalidate()
        End Set
    End Property

    ''' <summary>
    ''' True while the user is actively dragging (move/resize/pan/draw) or a move is pending.
    ''' Canvas should defer heavy scroll work until interaction ends.
    ''' </summary>
    Public ReadOnly Property IsInteracting As Boolean
        Get
            Return _mode <> InteractMode.None OrElse _pendingMove OrElse _pendingTextClick
        End Get
    End Property

    ''' <summary>
    ''' Applies current drawing strip color/size to the selected annotation (if any).
    ''' </summary>
    Public Function ApplyStyleToSelectedAnnotation(settings As DrawingSettings) As Boolean
        If settings Is Nothing OrElse _selectedAnnotation Is Nothing Then Return False
        Dim before = _selectedAnnotation.Clone()
        _selectedAnnotation.Color = Color.FromArgb(255, settings.BaseColor)
        If TypeOf _selectedAnnotation Is TextAnnotation Then
            _selectedAnnotation.NativeSize = AnnotationHelper.ClampFontSize(settings.Thickness)
        Else
            _selectedAnnotation.NativeSize = DrawingHelper.ClampThickness(settings.Thickness)
        End If
        Dim after = _selectedAnnotation.Clone()
        If Not AnnotationStatesEqual(before, after) Then
            _canvas?.RecordAnnotationChanged(ItemId, before, after)
            Invalidate()
            Return True
        End If
        Return False
    End Function

    Public Event PositionChanged As EventHandler(Of PositionChangedEventArgs)
    Public Event TransformChanged As EventHandler(Of TransformChangedEventArgs)
    Public Event SelectedChanged As EventHandler
    Public Event InteractionEnded As EventHandler

    ''' <summary>
    ''' Cancels any in-progress move/resize/draw (e.g. another screenshot took the click).
    ''' </summary>
    Public Sub CancelInteraction()
        If _mode = InteractMode.None AndAlso Not _pendingMove Then Return
        Dim wasMoveOrResize = (_mode = InteractMode.Move OrElse _mode = InteractMode.Resize)
        _pendingMove = False
        If Capture Then Capture = False
        _mode = InteractMode.None
        _resizeEdge = ResizeEdge.None
        _activeStroke = Nothing
        _annotationDraft = Nothing
        _pendingTextClick = False
        _annotEditKind = AnnotEditKind.None
        _annotEditOriginal = Nothing
        _drawLockHorizontal = False
        _hasTransformBaseline = False
        Cursor = CursorForIdle(PointToClient(Cursor.Position))
        If wasMoveOrResize Then
            Dim clamped = CanvasCoordinateHelper.ClampDocumentLocation(Location, Size)
            If clamped <> Location Then Location = clamped
            RaiseEvent InteractionEnded(Me, EventArgs.Empty)
        End If
    End Sub

    ''' <summary>
    ''' Called when mouse is released anywhere on the canvas — finishes a select-click
    ''' that never became a drag (no Capture was taken).
    ''' </summary>
    Public Sub NotifyGlobalMouseUp()
        If _pendingTextClick AndAlso _mode = InteractMode.None Then
            EndInteract()
        ElseIf _pendingMove AndAlso _mode = InteractMode.None Then
            _pendingMove = False
            Cursor = CursorForIdle(PointToClient(Cursor.Position))
        ElseIf _mode <> InteractMode.None Then
            EndInteract()
        End If
    End Sub

    Public Sub New(itemId As Guid, image As Image, canvas As ScreenshotCanvas)
        If image Is Nothing Then Throw New ArgumentNullException(NameOf(image))
        If canvas Is Nothing Then Throw New ArgumentNullException(NameOf(canvas))

        _itemId = itemId
        _image = image
        _canvas = canvas
        _naturalSize = image.Size
        _zoom = ZoomHelper.DefaultZoom
        _aspectReference = _naturalSize

        SetStyle(ControlStyles.AllPaintingInWmPaint Or
                 ControlStyles.UserPaint Or
                 ControlStyles.OptimizedDoubleBuffer Or
                 ControlStyles.ResizeRedraw Or
                 ControlStyles.Selectable Or
                 ControlStyles.StandardClick Or
                 ControlStyles.StandardDoubleClick, True)
        UpdateStyles()

        BackColor = Color.FromArgb(40, 40, 44)
        Cursor = Cursors.SizeAll
        TabStop = True
        Size = New Size(Math.Max(MinDisplayPx, _naturalSize.Width), Math.Max(MinDisplayPx, _naturalSize.Height))
        RecenterPan()
    End Sub

    ''' <summary>
    ''' Called when the canvas tool changes so the cursor updates.
    ''' </summary>
    Public Sub NotifyToolChanged()
        If _mode = InteractMode.None Then
            Cursor = CursorForIdle()
        End If
    End Sub

    Public Function CaptureTransform() As BoxTransformState
        Return BoxTransformState.FromBox(Me)
    End Function

    Public Sub ApplyTransform(state As BoxTransformState)
        _zoom = ZoomHelper.ClampZoom(state.Zoom)
        Location = CanvasCoordinateHelper.ClampDocumentLocation(state.Location, state.Size)
        Size = New Size(Math.Max(MinDisplayPx, state.Size.Width), Math.Max(MinDisplayPx, state.Size.Height))
        Dim content = ZoomHelper.ContentSize(Size, _zoom)
        _pan = ZoomHelper.ClampPan(state.Pan, content, Size)
        Invalidate()
    End Sub

    Public Sub AddStroke(stroke As InkStroke)
        If stroke Is Nothing Then Return
        _strokes.Add(stroke)
        Invalidate()
    End Sub

    Public Function RemoveStroke(stroke As InkStroke) As Boolean
        If stroke Is Nothing Then Return False
        Dim removed = _strokes.Remove(stroke)
        If removed Then Invalidate()
        Return removed
    End Function

    Public Function CloneStrokes() As List(Of InkStroke)
        ' Strokes are immutable after completion; share references for undo snapshots.
        Return New List(Of InkStroke)(_strokes)
    End Function

    Public Sub ReplaceStrokes(strokes As IEnumerable(Of InkStroke))
        _strokes.Clear()
        If strokes IsNot Nothing Then
            _strokes.AddRange(strokes)
        End If
        Invalidate()
    End Sub

    Public Sub AddAnnotation(annotation As AnnotationBase)
        If annotation Is Nothing Then Return
        _annotations.Add(annotation)
        Invalidate()
    End Sub

    Public Function RemoveAnnotation(annotation As AnnotationBase) As Boolean
        If annotation Is Nothing Then Return False
        Dim removed = _annotations.Remove(annotation)
        If Object.ReferenceEquals(_selectedAnnotation, annotation) Then
            _selectedAnnotation = Nothing
        End If
        If removed Then Invalidate()
        Return removed
    End Function

    Public Function CloneAnnotations() As List(Of AnnotationBase)
        Dim list As New List(Of AnnotationBase)(_annotations.Count)
        For Each a In _annotations
            list.Add(a.Clone())
        Next
        Return list
    End Function

    ''' <summary>Snapshot clones for undo; shares instances currently on the box.</summary>
    Public Function GetAnnotations() As List(Of AnnotationBase)
        Return New List(Of AnnotationBase)(_annotations)
    End Function

    Public Sub ReplaceAnnotations(annotations As IEnumerable(Of AnnotationBase))
        _annotations.Clear()
        _selectedAnnotation = Nothing
        If annotations IsNot Nothing Then
            _annotations.AddRange(annotations)
        End If
        Invalidate()
    End Sub

    Public ReadOnly Property SelectedAnnotation As AnnotationBase
        Get
            Return _selectedAnnotation
        End Get
    End Property

    Private Sub RememberTransformBaseline()
        _transformAtInteractionStart = CaptureTransform()
        _hasTransformBaseline = True
    End Sub

    Public Sub ZoomIn()
        Dim before = CaptureTransform()
        Dim center As New Point(Width \ 2, Height \ 2)
        SetZoom(ZoomHelper.ZoomBySteps(_zoom, 1), center, useCursor:=True, notify:=True)
        Dim after = CaptureTransform()
        If Not before.EqualsState(after) Then _canvas?.RecordTransform(ItemId, before, after)
    End Sub

    Public Sub ZoomOut()
        Dim before = CaptureTransform()
        Dim center As New Point(Width \ 2, Height \ 2)
        SetZoom(ZoomHelper.ZoomBySteps(_zoom, -1), center, useCursor:=True, notify:=True)
        Dim after = CaptureTransform()
        If Not before.EqualsState(after) Then _canvas?.RecordTransform(ItemId, before, after)
    End Sub

    Public Sub ZoomReset()
        Dim before = CaptureTransform()
        SetZoom(ZoomHelper.DefaultZoom, Point.Empty, useCursor:=False, notify:=True)
        RecenterPan()
        Invalidate()
        Dim after = CaptureTransform()
        If Not before.EqualsState(after) Then _canvas?.RecordTransform(ItemId, before, after)
    End Sub

    Public Sub ZoomTo(zoom As Double)
        Dim before = CaptureTransform()
        Dim center As New Point(Width \ 2, Height \ 2)
        SetZoom(zoom, center, useCursor:=True, notify:=True)
        Dim after = CaptureTransform()
        If Not before.EqualsState(after) Then _canvas?.RecordTransform(ItemId, before, after)
    End Sub

    ''' <summary>
    ''' Applies a vertical wheel delta as zoom (toolbar / callers that still want zoom).
    ''' </summary>
    Public Sub HandleWheelZoom(wheelDelta As Integer, screenPoint As Point)
        Dim steps = Math.Sign(wheelDelta)
        If steps = 0 Then Return
        Dim local = PointToClient(screenPoint)
        If Not ClientRectangle.Contains(local) Then
            local = New Point(Width \ 2, Height \ 2)
        End If
        Dim before = CaptureTransform()
        SetZoom(ZoomHelper.ZoomBySteps(_zoom, steps), local, useCursor:=True, notify:=True)
        Dim after = CaptureTransform()
        If Not before.EqualsState(after) Then
            _canvas?.RecordTransform(ItemId, before, after)
        End If
    End Sub

    ''' <summary>
    ''' True when zoomed content is larger than the frame on the given axis (can pan).
    ''' </summary>
    Public Function CanPanContent(horizontal As Boolean) As Boolean
        Dim content = ZoomHelper.ContentSize(Size, _zoom)
        If horizontal Then
            Return content.Width > Width
        End If
        Return content.Height > Height
    End Function

    ''' <summary>
    ''' Pans zoomed content with the wheel. Positive delta pans content so you see "up/left".
    ''' Returns True if pan was applied.
    ''' </summary>
    Public Function HandleWheelPan(wheelDelta As Integer, horizontal As Boolean) As Boolean
        Dim pixels = WheelScrollHelper.DeltaToScrollPixels(wheelDelta)
        If pixels = 0 Then Return False
        If Not CanPanContent(horizontal) Then Return False

        Dim content = ZoomHelper.ContentSize(Size, _zoom)
        Dim dx = 0
        Dim dy = 0
        If horizontal Then
            dx = -pixels
        Else
            ' Match form scroll feel: wheel up (positive) moves view up → pan.Y increases
            dy = pixels
        End If
        Dim nextPan = ZoomHelper.ClampPan(New Point(_pan.X + dx, _pan.Y + dy), content, Size)
        If nextPan = _pan Then Return False
        Dim before = CaptureTransform()
        _pan = nextPan
        Invalidate()
        Dim after = CaptureTransform()
        If Not before.EqualsState(after) Then
            _canvas?.RecordTransform(ItemId, before, after)
        End If
        Return True
    End Function

    ''' <summary>Horizontal pan of zoomed content (side-tilt + Ctrl).</summary>
    Public Sub HandleWheelPanHorizontal(wheelDelta As Integer)
        HandleWheelPan(wheelDelta, horizontal:=True)
    End Sub

    ''' <summary>
    ''' Draws this screenshot as currently displayed (zoom/pan/stretch), without selection chrome.
    ''' Used when exporting the tab composite.
    ''' </summary>
    Public Sub DrawContentAsDisplayed(g As Graphics, destFrame As Rectangle)
        If g Is Nothing OrElse _image Is Nothing Then Return
        If destFrame.Width <= 0 OrElse destFrame.Height <= 0 Then Return

        Dim state = g.Save()
        Try
            g.SetClip(destFrame)
            g.InterpolationMode = If(_zoom > 1.01,
                Drawing2D.InterpolationMode.NearestNeighbor,
                Drawing2D.InterpolationMode.HighQualityBicubic)
            g.PixelOffsetMode = Drawing2D.PixelOffsetMode.Half

            ' Scale pan/content from current frame size to destFrame size (usually 1:1)
            Dim scaleX = destFrame.Width / CDbl(Math.Max(1, Width))
            Dim scaleY = destFrame.Height / CDbl(Math.Max(1, Height))
            Dim content = ZoomHelper.ContentSize(Size, _zoom)
            Dim contentW = Math.Max(1, CInt(Math.Round(content.Width * scaleX)))
            Dim contentH = Math.Max(1, CInt(Math.Round(content.Height * scaleY)))
            Dim panX = CInt(Math.Round(_pan.X * scaleX))
            Dim panY = CInt(Math.Round(_pan.Y * scaleY))

            Using bg As New SolidBrush(BackColor)
                g.FillRectangle(bg, destFrame)
            End Using

            Dim dest As New Rectangle(
                destFrame.X + panX,
                destFrame.Y + panY,
                contentW,
                contentH)
            g.DrawImage(_image, dest)
            DrawAllStrokes(g, destFrame, scaleX, scaleY)
            DrawAllAnnotations(g, destFrame, scaleX, scaleY, selectedId:=Guid.Empty)
        Finally
            g.Restore(state)
        End Try
    End Sub

    Private Sub SetZoom(value As Double, cursorInViewport As Point, useCursor As Boolean, notify As Boolean)
        Dim z = ZoomHelper.ClampZoom(value)
        If Math.Abs(z - _zoom) < 0.0001 Then Return

        Dim oldZoom = _zoom
        _zoom = z
        If useCursor Then
            _pan = ZoomHelper.PanAfterZoom(oldZoom, _zoom, _pan, cursorInViewport, Size)
        Else
            RecenterPan()
        End If
        Invalidate()
        If notify Then RaiseTransform()
    End Sub

    Private Sub RecenterPan()
        Dim content = ZoomHelper.ContentSize(Size, _zoom)
        _pan = ZoomHelper.ClampPan(New Point(0, 0), content, Size)
        ' When zoomed in, clamp of (0,0) is correct; when zoomed out, centers
        If content.Width > Width OrElse content.Height > Height Then
            _pan = ZoomHelper.ClampPan(
                New Point((Width - content.Width) \ 2, (Height - content.Height) \ 2),
                content, Size)
        End If
    End Sub

    Private Sub RaiseTransform()
        RaiseEvent TransformChanged(Me, New TransformChangedEventArgs(_itemId, Location, Size, _zoom))
    End Sub

    Private Sub RaisePosition()
        RaiseEvent PositionChanged(Me, New PositionChangedEventArgs(_itemId, Location))
    End Sub

    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        Dim g = e.Graphics
        g.CompositingMode = Drawing2D.CompositingMode.SourceCopy
        g.CompositingQuality = Drawing2D.CompositingQuality.HighSpeed
        g.InterpolationMode = If(_zoom > 1.01,
            Drawing2D.InterpolationMode.NearestNeighbor,
            Drawing2D.InterpolationMode.Low)
        g.PixelOffsetMode = Drawing2D.PixelOffsetMode.Half
        g.SmoothingMode = Drawing2D.SmoothingMode.None

        ' Background (letterbox / gaps when zoomed out)
        Using bg As New SolidBrush(BackColor)
            g.FillRectangle(bg, ClientRectangle)
        End Using

        Dim content = ZoomHelper.ContentSize(Size, _zoom)
        Dim dest As New Rectangle(_pan.X, _pan.Y, content.Width, content.Height)
        g.CompositingMode = Drawing2D.CompositingMode.SourceOver
        g.DrawImage(_image, dest)

        ' Ink + shapes above the image, below selection chrome
        g.SmoothingMode = Drawing2D.SmoothingMode.AntiAlias
        Dim paintFrame As New Rectangle(0, 0, Width, Height)
        DrawAllStrokes(g, paintFrame, 1.0, 1.0)
        If _activeStroke IsNot Nothing Then
            DrawStroke(g, _activeStroke, paintFrame, 1.0, 1.0)
        End If
        Dim selId = If(_selectedAnnotation IsNot Nothing, _selectedAnnotation.Id, Guid.Empty)
        DrawAllAnnotations(g, paintFrame, 1.0, 1.0, selId)
        If _annotationDraft IsNot Nothing Then
            DrawOneAnnotation(g, _annotationDraft, paintFrame, 1.0, 1.0, selected:=False)
        End If
        g.SmoothingMode = Drawing2D.SmoothingMode.None

        ' Frame chrome
        If _selected Then
            Using pen As New Pen(Color.FromArgb(255, 0, 120, 215), 2.0F)
                g.DrawRectangle(pen, 1, 1, Width - 3, Height - 3)
            End Using
            Using brush As New SolidBrush(Color.FromArgb(255, 0, 120, 215))
                Dim s = Grip
                g.FillRectangle(brush, Width - s, Height - s, s, s)
                g.FillRectangle(brush, 0, 0, s, s)
                g.FillRectangle(brush, Width - s, 0, s, s)
                g.FillRectangle(brush, 0, Height - s, s, s)
            End Using
        Else
            Using pen As New Pen(Color.FromArgb(160, 100, 100, 100), 1.0F)
                g.DrawRectangle(pen, 0, 0, Width - 1, Height - 1)
            End Using
        End If
    End Sub

    Private Sub DrawAllStrokes(g As Graphics, destFrame As Rectangle, scaleX As Double, scaleY As Double)
        For Each stroke In _strokes
            DrawStroke(g, stroke, destFrame, scaleX, scaleY)
        Next
    End Sub

    Private Sub DrawStroke(g As Graphics, stroke As InkStroke, destFrame As Rectangle, scaleX As Double, scaleY As Double)
        If stroke Is Nothing OrElse stroke.Points.Count = 0 Then Return

        Dim content = ZoomHelper.ContentSize(Size, _zoom)
        Dim panX = CSng(_pan.X * scaleX)
        Dim panY = CSng(_pan.Y * scaleY)
        Dim contentW = Math.Max(1.0F, CSng(content.Width * scaleX))
        Dim contentH = Math.Max(1.0F, CSng(content.Height * scaleY))
        Dim width = Math.Max(1.0F,
            DrawingHelper.ViewportStrokeWidth(stroke.NativeWidth, Size, _naturalSize, _zoom) *
            CSng((scaleX + scaleY) / 2.0))

        Dim pts = BuildViewportPoints(stroke, destFrame, panX, panY, contentW, contentH)
        If pts.Length = 0 Then Return

        Dim prevMode = g.CompositingMode
        Dim prevSmooth = g.SmoothingMode
        Dim prevPixel = g.PixelOffsetMode
        Try
            g.CompositingMode = Drawing2D.CompositingMode.SourceOver
            g.SmoothingMode = Drawing2D.SmoothingMode.AntiAlias
            g.PixelOffsetMode = Drawing2D.PixelOffsetMode.HighQuality

            ' Always round caps so highlighter start/stop is flush (flat caps look chiseled/offset)
            Using pen As New Pen(stroke.Color, width)
                pen.StartCap = Drawing2D.LineCap.Round
                pen.EndCap = Drawing2D.LineCap.Round
                pen.LineJoin = Drawing2D.LineJoin.Round
                pen.MiterLimit = 2.0F

                If pts.Length = 1 Then
                    Dim r = width / 2.0F
                    Using br As New SolidBrush(stroke.Color)
                        g.FillEllipse(br, pts(0).X - r, pts(0).Y - r, width, width)
                    End Using
                Else
                    ' Single path stroke avoids joint double-alpha artifacts from DrawLines
                    Using path As New Drawing2D.GraphicsPath()
                        path.AddLines(pts)
                        g.DrawPath(pen, path)
                    End Using
                End If
            End Using
        Finally
            g.CompositingMode = prevMode
            g.SmoothingMode = prevSmooth
            g.PixelOffsetMode = prevPixel
        End Try
    End Sub

    Private Shared Function BuildViewportPoints(
        stroke As InkStroke,
        destFrame As Rectangle,
        panX As Single,
        panY As Single,
        contentW As Single,
        contentH As Single) As PointF()

        Dim list As New List(Of PointF)(stroke.Points.Count)
        Dim lastX As Single = Single.NaN
        Dim lastY As Single = Single.NaN
        For Each p In stroke.Points
            Dim pt As New PointF(
                destFrame.X + panX + p.X * contentW,
                destFrame.Y + panY + p.Y * contentH)
            ' Drop exact duplicates that create zero-length segments / end artifacts
            If list.Count = 0 OrElse Math.Abs(pt.X - lastX) > 0.01F OrElse Math.Abs(pt.Y - lastY) > 0.01F Then
                list.Add(pt)
                lastX = pt.X
                lastY = pt.Y
            End If
        Next
        Return list.ToArray()
    End Function

    Protected Overrides Sub OnPaintBackground(e As PaintEventArgs)
        ' Fully painted in OnPaint — avoids flicker/ghost trails
    End Sub

    Protected Overrides Sub OnMouseDown(e As MouseEventArgs)
        MyBase.OnMouseDown(e)
        If e.Button = MouseButtons.Left Then
            ' Stop any stuck interaction on this or other boxes before starting a new one
            If _mode <> InteractMode.None OrElse _pendingMove Then
                CancelInteraction()
            End If
            _canvas?.CancelInteractionsExcept(Me)

            SelectMe()
            BringToFront()
            Dim edge = HitTestEdge(e.Location)
            Dim ctrlHeld = (Control.ModifierKeys And Keys.Control) = Keys.Control
            Dim shiftHeld = (Control.ModifierKeys And Keys.Shift) = Keys.Shift

            If edge <> ResizeEdge.None Then
                _mode = InteractMode.Resize
                _resizeEdge = edge
                _resizeStartCursor = PointToScreen(e.Location)
                _resizeStartLocation = Location
                _resizeStartSize = Size
                _aspectReference = Size
                RememberTransformBaseline()
                Capture = True
            ElseIf ctrlHeld Then
                ' Ctrl+drag always moves (works while a draw tool is active)
                ClearAnnotationSelection()
                BeginPendingMove(e.Location)
            ElseIf DrawingHelper.IsInkTool(_canvas.ActiveTool) Then
                ClearAnnotationSelection()
                BeginDraw(e.Location, lockHorizontal:=shiftHeld)
            ElseIf DrawingHelper.IsShapeTool(_canvas.ActiveTool) Then
                ClearAnnotationSelection()
                BeginAnnotate(e.Location, _canvas.ActiveTool)
            ElseIf shiftHeld Then
                ClearAnnotationSelection()
                BeginPan(e.Location)
            ElseIf TryBeginAnnotationEdit(e.Location) Then
                ' Pointer hit an annotation — edit it
            Else
                ' Click selects screenshot; drag past threshold moves
                ClearAnnotationSelection()
                BeginPendingMove(e.Location)
            End If
        ElseIf e.Button = MouseButtons.Middle Then
            _canvas?.CancelInteractionsExcept(Me)
            SelectMe()
            BringToFront()
            BeginPan(e.Location)
        End If
    End Sub

    Private Sub BeginPan(local As Point)
        _mode = InteractMode.Pan
        _panStartCursor = PointToScreen(local)
        _panStart = _pan
        RememberTransformBaseline()
        Capture = True
        Cursor = Cursors.SizeAll
    End Sub

    Private Sub BeginPendingMove(local As Point)
        _pendingMove = True
        _pendingMoveLocal = local
        _pendingMoveScreen = PointToScreen(local)
        _mode = InteractMode.None
        ' Do NOT capture yet — capture steals later clicks and teleports this control
        ' when the pointer moves toward another screenshot.
        Capture = False
        Cursor = CursorForIdle(local)
    End Sub

    Private Sub BeginMove(local As Point)
        _pendingMove = False
        _mode = InteractMode.Move
        _dragOffset = local
        RememberTransformBaseline()
        Capture = True
        Cursor = Cursors.SizeAll
    End Sub

    ''' <summary>
    ''' Screen point → location of this control's top-left on its parent surface.
    ''' Parent is a non-AutoScroll surface, so PointToClient is sufficient.
    ''' </summary>
    Private Function ParentLocationFromScreen(screenCursor As Point, dragOffsetLocal As Point) As Point
        Dim parentCtrl = Parent
        If parentCtrl Is Nothing Then Return Location
        Dim clientPt = parentCtrl.PointToClient(screenCursor)
        Return New Point(clientPt.X - dragOffsetLocal.X, clientPt.Y - dragOffsetLocal.Y)
    End Function

    Private Sub BeginDraw(local As Point, Optional lockHorizontal As Boolean = False)
        _mode = InteractMode.Draw
        _drawLockHorizontal = lockHorizontal OrElse
            (Control.ModifierKeys And Keys.Shift) = Keys.Shift
        _activeStroke = _canvas.DrawingSettings.CreateStroke()
        AppendStrokePoint(local, force:=True)
        Capture = True
        Cursor = Cursors.Cross
        Invalidate()
    End Sub

    ''' <summary>
    ''' Adds a sample to the active stroke. force=True always records (stroke start/end).
    ''' Shift (or stroke lock) keeps Y equal to the first point — horizontal straight line.
    ''' </summary>
    Private Sub AppendStrokePoint(local As Point, Optional force As Boolean = False)
        If _activeStroke Is Nothing Then Return
        Dim norm = DrawingHelper.ViewportToNormalized(local, Size, _pan, _zoom)
        If Not norm.HasValue Then Return

        Dim p = DrawingHelper.ClampNormalized(norm.Value)

        Dim shiftHeld = (Control.ModifierKeys And Keys.Shift) = Keys.Shift
        If (_drawLockHorizontal OrElse shiftHeld) AndAlso _activeStroke.Points.Count > 0 Then
            ' Horizontal constraint: same Y as stroke origin
            p = DrawingHelper.ConstrainHorizontal(p, _activeStroke.Points(0).Y)
            _drawLockHorizontal = True
        End If

        If _activeStroke.Points.Count = 0 OrElse force Then
            ' When forcing end point under horizontal lock, still pin Y
            If force AndAlso _drawLockHorizontal AndAlso _activeStroke.Points.Count > 0 Then
                p = New PointF(p.X, _activeStroke.Points(0).Y)
            End If
            If _activeStroke.Points.Count = 0 Then
                _activeStroke.Points.Add(p)
            ElseIf force Then
                ' Replace last or append end — avoid stacking duplicates on same spot
                Dim last = _activeStroke.Points(_activeStroke.Points.Count - 1)
                If Math.Abs(last.X - p.X) > 0.00001F OrElse Math.Abs(last.Y - p.Y) > 0.00001F Then
                    _activeStroke.Points.Add(p)
                End If
            End If
            Return
        End If

        Dim prev = _activeStroke.Points(_activeStroke.Points.Count - 1)
        Dim dx = p.X - prev.X
        Dim dy = p.Y - prev.Y
        ' Keep early samples denser for a clean start; thin out later
        Dim minDistSq = If(_activeStroke.Points.Count < 4, 0.00000005F, 0.0000002F)
        If (dx * dx + dy * dy) >= minDistSq Then
            _activeStroke.Points.Add(p)
        End If
    End Sub

    Protected Overrides Sub OnMouseMove(e As MouseEventArgs)
        MyBase.OnMouseMove(e)

        If _pendingMove Then
            ' Use Cursor.Position (screen) so we never interpret out-of-bounds e.Location
            Dim screenCursor = Cursor.Position
            Dim dx = screenCursor.X - _pendingMoveScreen.X
            Dim dy = screenCursor.Y - _pendingMoveScreen.Y
            If (Control.MouseButtons And MouseButtons.Left) <> MouseButtons.Left Then
                ' Button released elsewhere — abandon pending move (select only)
                _pendingMove = False
                Return
            End If
            If dx * dx + dy * dy >= MoveDragThresholdPx * MoveDragThresholdPx Then
                BeginMove(_pendingMoveLocal)
                ' Fall through into Move handling
            Else
                Return
            End If
        End If

        If _mode = InteractMode.None Then
            Cursor = CursorForIdle(e.Location)
            Return
        End If

        Dim screenPt = Cursor.Position

        If _mode = InteractMode.Draw Then
            If (Control.ModifierKeys And Keys.Shift) = Keys.Shift Then
                _drawLockHorizontal = True
            End If
            Dim before = If(_activeStroke IsNot Nothing, _activeStroke.Points.Count, 0)
            AppendStrokePoint(e.Location, force:=False)
            If _activeStroke IsNot Nothing AndAlso _activeStroke.Points.Count <> before Then
                Invalidate()
            End If
            Return
        End If

        If _mode = InteractMode.Annotate Then
            UpdateAnnotateDraft(e.Location)
            Return
        End If

        If _mode = InteractMode.AnnotEdit Then
            UpdateAnnotationEdit(e.Location)
            Return
        End If

        If _mode = InteractMode.Move Then
            Cursor = Cursors.SizeAll
            Dim newLoc = ParentLocationFromScreen(screenPt, _dragOffset)
            newLoc = CanvasCoordinateHelper.ClampDocumentLocation(newLoc, Size)
            If newLoc <> Location Then
                SetBounds(newLoc.X, newLoc.Y, Width, Height, BoundsSpecified.Location)
            End If

        ElseIf _mode = InteractMode.Pan Then
            Dim dx = screenPt.X - _panStartCursor.X
            Dim dy = screenPt.Y - _panStartCursor.Y
            Dim content = ZoomHelper.ContentSize(Size, _zoom)
            _pan = ZoomHelper.ClampPan(New Point(_panStart.X + dx, _panStart.Y + dy), content, Size)
            Invalidate()

        ElseIf _mode = InteractMode.Resize Then
            Dim dx = screenPt.X - _resizeStartCursor.X
            Dim dy = screenPt.Y - _resizeStartCursor.Y
            Dim tentative As New Size(_resizeStartSize.Width, _resizeStartSize.Height)

            Select Case _resizeEdge
                Case ResizeEdge.E, ResizeEdge.NE, ResizeEdge.SE
                    tentative.Width = _resizeStartSize.Width + dx
                Case ResizeEdge.W, ResizeEdge.NW, ResizeEdge.SW
                    tentative.Width = _resizeStartSize.Width - dx
            End Select

            Select Case _resizeEdge
                Case ResizeEdge.S, ResizeEdge.SE, ResizeEdge.SW
                    tentative.Height = _resizeStartSize.Height + dy
                Case ResizeEdge.N, ResizeEdge.NE, ResizeEdge.NW
                    tentative.Height = _resizeStartSize.Height - dy
            End Select

            Dim cornerDrag = IsCornerResize(_resizeEdge)
            Dim display As Size
            If cornerDrag Then
                display = ZoomHelper.AspectPreserveSize(_aspectReference, tentative)
                display = ZoomHelper.FreeResizeSize(display, MinDisplayPx)
            Else
                display = ZoomHelper.FreeResizeSize(tentative, MinDisplayPx)
            End If

            Dim newLoc = _resizeStartLocation
            Select Case _resizeEdge
                Case ResizeEdge.W, ResizeEdge.NW, ResizeEdge.SW
                    newLoc.X = _resizeStartLocation.X + (_resizeStartSize.Width - display.Width)
            End Select
            Select Case _resizeEdge
                Case ResizeEdge.N, ResizeEdge.NE, ResizeEdge.NW
                    newLoc.Y = _resizeStartLocation.Y + (_resizeStartSize.Height - display.Height)
            End Select
            newLoc = CanvasCoordinateHelper.ClampDocumentLocation(newLoc, display)

            SetBounds(newLoc.X, newLoc.Y, display.Width, display.Height, BoundsSpecified.All)
            Dim content = ZoomHelper.ContentSize(Size, _zoom)
            _pan = ZoomHelper.ClampPan(_pan, content, Size)
            Invalidate()
        End If
    End Sub

    Protected Overrides Sub OnMouseUp(e As MouseEventArgs)
        MyBase.OnMouseUp(e)
        EndInteract()
    End Sub

    Protected Overrides Sub OnMouseLeave(e As EventArgs)
        MyBase.OnMouseLeave(e)
        ' Pending select-click: leaving the control without starting a drag = cancel pending
        If _pendingMove AndAlso _mode = InteractMode.None AndAlso
           (Control.MouseButtons And MouseButtons.Left) <> MouseButtons.Left Then
            _pendingMove = False
        End If
    End Sub

    Protected Overrides Sub OnMouseCaptureChanged(e As EventArgs)
        MyBase.OnMouseCaptureChanged(e)
        ' If capture is lost mid-drag, stop stuck moves/resizes (not pending-only select)
        If Not Capture AndAlso _mode <> InteractMode.None AndAlso Not _endingInteract Then
            EndInteract()
        End If
    End Sub

    Protected Overrides Sub OnMouseWheel(e As MouseEventArgs)
        HandleVerticalWheel(e.Delta)
        MarkWheelHandled(e)
    End Sub

    ''' <summary>
    ''' Wheel routing over a screenshot:
    ''' - Shift + vertical wheel → zoom this image
    ''' - Ctrl + vertical/side-tilt → pan zoomed content (else form scroll)
    ''' - No modifiers → form scroll (vertical or side-tilt horizontal)
    ''' </summary>
    Protected Overrides Sub WndProc(ByRef m As Message)
        Const WM_MOUSEWHEEL As Integer = &H20A
        Const WM_MOUSEHWHEEL As Integer = &H20E

        If m.Msg = WM_MOUSEWHEEL Then
            Dim delta = WheelScrollHelper.DeltaFromWParam(m.WParam)
            HandleVerticalWheel(delta)
            m.Result = New IntPtr(1)
            Return
        End If

        If m.Msg = WM_MOUSEHWHEEL Then
            Dim delta = WheelScrollHelper.DeltaFromWParam(m.WParam)
            HandleHorizontalWheel(delta)
            m.Result = New IntPtr(1)
            Return
        End If

        If m.Msg = MouseActivateHelper.WM_MOUSEACTIVATE Then
            MyBase.WndProc(m)
            m.Result = MouseActivateHelper.ActivateAndPassClick
            Return
        End If

        MyBase.WndProc(m)
    End Sub

    Private Sub HandleVerticalWheel(delta As Integer)
        Dim ctrl = (Control.ModifierKeys And Keys.Control) = Keys.Control
        Dim shift = (Control.ModifierKeys And Keys.Shift) = Keys.Shift

        ' Shift+wheel → zoom (wins over Ctrl if both held)
        If shift Then
            HandleWheelZoom(delta, Cursor.Position)
            Return
        End If

        If ctrl AndAlso HandleWheelPan(delta, horizontal:=False) Then
            Return
        End If
        ForwardVerticalScrollToCanvas(delta)
    End Sub

    Private Sub HandleHorizontalWheel(delta As Integer)
        Dim ctrl = (Control.ModifierKeys And Keys.Control) = Keys.Control
        ' Side-tilt: Ctrl pans zoomed content; otherwise form horizontal scroll
        If ctrl AndAlso HandleWheelPan(delta, horizontal:=True) Then
            Return
        End If
        ForwardHorizontalScrollToCanvas(delta)
    End Sub

    Private Sub ForwardVerticalScrollToCanvas(wheelDelta As Integer)
        ' Parent is the surface panel; scroll the owning canvas
        _canvas?.ScrollFromWheel(wheelDelta, horizontal:=False)
    End Sub

    Private Sub ForwardHorizontalScrollToCanvas(wheelDelta As Integer)
        _canvas?.ScrollFromWheel(wheelDelta, horizontal:=True)
    End Sub

    Private Shared Sub MarkWheelHandled(e As MouseEventArgs)
        Dim handled = TryCast(e, HandledMouseEventArgs)
        If handled IsNot Nothing Then
            handled.Handled = True
        End If
    End Sub

    Protected Overrides Sub OnDoubleClick(e As EventArgs)
        MyBase.OnDoubleClick(e)
        ZoomReset()
    End Sub

    Protected Overrides Function IsInputKey(keyData As Keys) As Boolean
        Dim key = keyData And Not Keys.Modifiers
        If key = Keys.Delete OrElse key = Keys.Back Then Return True
        If key = Keys.ControlKey OrElse key = Keys.ShiftKey OrElse
           key = Keys.LControlKey OrElse key = Keys.RControlKey OrElse
           key = Keys.LShiftKey OrElse key = Keys.RShiftKey Then
            Return True
        End If
        Return MyBase.IsInputKey(keyData)
    End Function

    Protected Overrides Sub OnKeyDown(e As KeyEventArgs)
        MyBase.OnKeyDown(e)
        If e.KeyCode = Keys.Delete OrElse e.KeyCode = Keys.Back Then
            If _selectedAnnotation IsNot Nothing Then
                Dim victim = _selectedAnnotation
                If RemoveAnnotation(victim) Then
                    _canvas?.RecordAnnotationRemoved(ItemId, victim)
                End If
                e.Handled = True
                e.SuppressKeyPress = True
                Return
            End If
            If _canvas IsNot Nothing AndAlso _canvas.RemoveSelectedScreenshot() Then
                e.Handled = True
                e.SuppressKeyPress = True
            End If
        End If
        ' Update cursor when Ctrl (move) or Shift is pressed without moving the mouse
        If _mode = InteractMode.None Then
            Cursor = CursorForIdle(PointToClient(Cursor.Position))
        ElseIf _mode = InteractMode.Move Then
            Cursor = Cursors.SizeAll
        ElseIf _mode = InteractMode.Draw AndAlso e.Shift Then
            _drawLockHorizontal = True
        End If
    End Sub

    Protected Overrides Sub OnKeyUp(e As KeyEventArgs)
        MyBase.OnKeyUp(e)
        If _mode = InteractMode.None Then
            Cursor = CursorForIdle(PointToClient(Cursor.Position))
        ElseIf _mode = InteractMode.Move Then
            Cursor = Cursors.SizeAll
        End If
    End Sub

    Protected Overrides Sub OnResize(e As EventArgs)
        MyBase.OnResize(e)
        Dim content = ZoomHelper.ContentSize(Size, _zoom)
        _pan = ZoomHelper.ClampPan(_pan, content, Size)
    End Sub

    Private Sub EndInteract()
        If _endingInteract Then Return
        If _mode = InteractMode.None AndAlso Not _pendingMove AndAlso Not _pendingTextClick Then Return
        _endingInteract = True
        Try
            Dim wasMove = _mode = InteractMode.Move
            Dim wasResize = _mode = InteractMode.Resize
            Dim wasPan = _mode = InteractMode.Pan
            Dim wasDraw = _mode = InteractMode.Draw
            Dim wasAnnotate = _mode = InteractMode.Annotate
            Dim wasAnnotEdit = _mode = InteractMode.AnnotEdit
            Dim pendingText = _pendingTextClick
            Dim textLocal = _pendingTextLocal
            ' Pure click (select only) — must not change Location or fire move events
            Dim wasPendingOnly = _pendingMove AndAlso _mode = InteractMode.None
            _pendingMove = False
            _pendingTextClick = False
            _mode = InteractMode.None
            _resizeEdge = ResizeEdge.None
            If Capture Then Capture = False

            Dim completedStroke As InkStroke = Nothing
            If wasDraw AndAlso _activeStroke IsNot Nothing Then
                AppendStrokePoint(PointToClient(Cursor.Position), force:=True)
                If _activeStroke.Points.Count >= 1 Then
                    _strokes.Add(_activeStroke)
                    completedStroke = _activeStroke
                End If
                _activeStroke = Nothing
                _drawLockHorizontal = False
                Invalidate()
            End If

            Dim completedAnnotation As AnnotationBase = Nothing
            If wasAnnotate Then
                completedAnnotation = CommitAnnotateDraft(PointToClient(Cursor.Position))
            End If
            If wasAnnotEdit Then
                CommitAnnotationEdit()
            End If
            ' Text tool: place only on a click (no meaningful drag)
            If pendingText AndAlso Not wasAnnotate AndAlso Not wasAnnotEdit Then
                Dim cur = PointToClient(Cursor.Position)
                Dim dx = cur.X - textLocal.X
                Dim dy = cur.Y - textLocal.Y
                If dx * dx + dy * dy <= MoveDragThresholdPx * MoveDragThresholdPx Then
                    PlaceTextAt(textLocal)
                End If
            End If

            ' Keep control in non-negative document space so AutoScroll can reach it
            If (wasMove OrElse wasResize) AndAlso Not wasPendingOnly Then
                Dim clamped = CanvasCoordinateHelper.ClampDocumentLocation(Location, Size)
                If clamped <> Location Then
                    Location = clamped
                End If
            End If

            If wasMove AndAlso Not wasPendingOnly Then
                RaisePosition()
            End If
            If wasResize Then
                RaiseTransform()
            End If

            ' Record undo for completed interactions
            If completedStroke IsNot Nothing Then
                _canvas?.RecordStroke(ItemId, completedStroke)
            End If
            If completedAnnotation IsNot Nothing Then
                _canvas?.RecordAnnotationAdded(ItemId, completedAnnotation)
            End If
            If _hasTransformBaseline AndAlso
                ((wasMove AndAlso Not wasPendingOnly) OrElse wasResize OrElse wasPan) Then
                Dim after = CaptureTransform()
                If Not _transformAtInteractionStart.EqualsState(after) Then
                    _canvas?.RecordTransform(ItemId, _transformAtInteractionStart, after)
                End If
            End If
            _hasTransformBaseline = False

            Cursor = CursorForIdle(PointToClient(Cursor.Position))
            ' Skip scroll-bounds churn on simple select clicks
            If Not wasPendingOnly OrElse wasDraw OrElse wasResize OrElse wasMove OrElse wasPan OrElse
               wasAnnotate OrElse wasAnnotEdit OrElse pendingText Then
                RaiseEvent InteractionEnded(Me, EventArgs.Empty)
            End If
        Finally
            _endingInteract = False
        End Try
    End Sub

    Private Function CursorForIdle(Optional local As Point? = Nothing) As Cursor
        If local.HasValue Then
            Dim edge = HitTestEdge(local.Value)
            If edge <> ResizeEdge.None Then Return CursorForEdge(edge)
        End If
        ' Ctrl held → move cursor (same as drag-to-move)
        If (Control.ModifierKeys And Keys.Control) = Keys.Control Then
            Return Cursors.SizeAll
        End If
        If _canvas IsNot Nothing AndAlso DrawingHelper.IsAnnotationTool(_canvas.ActiveTool) Then
            Return Cursors.Cross
        End If
        Return Cursors.SizeAll
    End Function

    Private Sub SelectMe()
        If _selected Then Return
        _selected = True
        Invalidate()
        RaiseEvent SelectedChanged(Me, EventArgs.Empty)
    End Sub

    Private Function HitTestEdge(local As Point) As ResizeEdge
        Dim nearL = local.X <= Grip
        Dim nearR = local.X >= Width - Grip
        Dim nearT = local.Y <= Grip
        Dim nearB = local.Y >= Height - Grip

        If nearT AndAlso nearL Then Return ResizeEdge.NW
        If nearT AndAlso nearR Then Return ResizeEdge.NE
        If nearB AndAlso nearL Then Return ResizeEdge.SW
        If nearB AndAlso nearR Then Return ResizeEdge.SE
        If nearT Then Return ResizeEdge.N
        If nearB Then Return ResizeEdge.S
        If nearL Then Return ResizeEdge.W
        If nearR Then Return ResizeEdge.E
        Return ResizeEdge.None
    End Function

    Private Shared Function IsCornerResize(edge As ResizeEdge) As Boolean
        Return edge = ResizeEdge.NE OrElse edge = ResizeEdge.NW OrElse
               edge = ResizeEdge.SE OrElse edge = ResizeEdge.SW
    End Function

    Private Shared Function CursorForEdge(edge As ResizeEdge) As Cursor
        Select Case edge
            Case ResizeEdge.N, ResizeEdge.S : Return Cursors.SizeNS
            Case ResizeEdge.E, ResizeEdge.W : Return Cursors.SizeWE
            Case ResizeEdge.NE, ResizeEdge.SW : Return Cursors.SizeNESW
            Case ResizeEdge.NW, ResizeEdge.SE : Return Cursors.SizeNWSE
            Case Else : Return Cursors.SizeAll
        End Select
    End Function

    Protected Overrides Sub Dispose(disposing As Boolean)
        ' Image lifetime owned by canvas — do not dispose _image here
        MyBase.Dispose(disposing)
    End Sub
End Class

Public Class PositionChangedEventArgs
    Inherits EventArgs

    Public Sub New(itemId As Guid, location As Point)
        Me.ItemId = itemId
        Me.Location = location
    End Sub

    Public ReadOnly Property ItemId As Guid
    Public ReadOnly Property Location As Point
End Class

Public Class TransformChangedEventArgs
    Inherits EventArgs

    Public Sub New(itemId As Guid, location As Point, size As Size, zoom As Double)
        Me.ItemId = itemId
        Me.Location = location
        Me.Size = size
        Me.Zoom = zoom
    End Sub

    Public ReadOnly Property ItemId As Guid
    Public ReadOnly Property Location As Point
    Public ReadOnly Property Size As Size
    Public ReadOnly Property Zoom As Double
End Class
