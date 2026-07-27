''' <summary>
''' Movable / resizable screenshot viewport.
''' - Frame size changes only via edge/corner drag (not zoom)
''' - Edge resize: free stretch; corner resize: aspect preserved
''' - Zoom scales image inside the fixed frame (pan follows cursor)
''' - Single custom-painted control (no child PictureBox) to avoid move ghosting
''' </summary>
Public Class MovableScreenshotBox
    Inherits Control

    Private Enum InteractMode
        None
        Move
        Resize
        Pan
        Draw
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

    Private Const Grip As Integer = 10
    Private Const MinDisplayPx As Integer = 48

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
    ''' True while the user is actively dragging (move/resize/pan). Canvas should defer heavy work.
    ''' </summary>
    Public ReadOnly Property IsInteracting As Boolean
        Get
            Return _mode <> InteractMode.None
        End Get
    End Property

    Public Event PositionChanged As EventHandler(Of PositionChangedEventArgs)
    Public Event TransformChanged As EventHandler(Of TransformChangedEventArgs)
    Public Event SelectedChanged As EventHandler
    Public Event InteractionEnded As EventHandler

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

    Public Sub ZoomIn()
        Dim center As New Point(Width \ 2, Height \ 2)
        SetZoom(ZoomHelper.ZoomBySteps(_zoom, 1), center, useCursor:=True, notify:=True)
    End Sub

    Public Sub ZoomOut()
        Dim center As New Point(Width \ 2, Height \ 2)
        SetZoom(ZoomHelper.ZoomBySteps(_zoom, -1), center, useCursor:=True, notify:=True)
    End Sub

    Public Sub ZoomReset()
        SetZoom(ZoomHelper.DefaultZoom, Point.Empty, useCursor:=False, notify:=True)
        RecenterPan()
        Invalidate()
    End Sub

    Public Sub ZoomTo(zoom As Double)
        Dim center As New Point(Width \ 2, Height \ 2)
        SetZoom(zoom, center, useCursor:=True, notify:=True)
    End Sub

    ''' <summary>
    ''' Applies a vertical wheel delta as zoom (frame size unchanged). Used when the
    ''' canvas receives the wheel message while the pointer is over this image.
    ''' </summary>
    Public Sub HandleWheelZoom(wheelDelta As Integer, screenPoint As Point)
        Dim steps = Math.Sign(wheelDelta)
        If steps = 0 Then Return
        Dim local = PointToClient(screenPoint)
        If Not ClientRectangle.Contains(local) Then
            local = New Point(Width \ 2, Height \ 2)
        End If
        SetZoom(ZoomHelper.ZoomBySteps(_zoom, steps), local, useCursor:=True, notify:=True)
    End Sub

    ''' <summary>
    ''' Horizontal wheel pans content inside the frame (does not scroll the parent canvas).
    ''' </summary>
    Public Sub HandleWheelPanHorizontal(wheelDelta As Integer)
        Dim dx = WheelScrollHelper.DeltaToScrollPixels(wheelDelta)
        If dx = 0 Then Return
        Dim content = ZoomHelper.ContentSize(Size, _zoom)
        _pan = ZoomHelper.ClampPan(New Point(_pan.X - dx, _pan.Y), content, Size)
        Invalidate()
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

        ' Ink (highlighter etc.) above the image, below selection chrome
        g.SmoothingMode = Drawing2D.SmoothingMode.AntiAlias
        DrawAllStrokes(g, New Rectangle(0, 0, Width, Height), 1.0, 1.0)
        If _activeStroke IsNot Nothing Then
            DrawStroke(g, _activeStroke, New Rectangle(0, 0, Width, Height), 1.0, 1.0)
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
                Capture = True
            ElseIf ctrlHeld Then
                ' Ctrl+drag always moves (works while a draw tool is active)
                BeginMove(e.Location)
            ElseIf shiftHeld Then
                _mode = InteractMode.Pan
                _panStartCursor = PointToScreen(e.Location)
                _panStart = _pan
                Capture = True
            ElseIf DrawingHelper.IsInkTool(_canvas.ActiveTool) Then
                BeginDraw(e.Location)
            Else
                BeginMove(e.Location)
            End If
        ElseIf e.Button = MouseButtons.Middle Then
            SelectMe()
            BringToFront()
            _mode = InteractMode.Pan
            _panStartCursor = PointToScreen(e.Location)
            _panStart = _pan
            Capture = True
        End If
    End Sub

    Private Sub BeginMove(local As Point)
        _mode = InteractMode.Move
        _dragOffset = local
        Capture = True
    End Sub

    Private Sub BeginDraw(local As Point)
        _mode = InteractMode.Draw
        _activeStroke = _canvas.DrawingSettings.CreateStroke()
        AppendStrokePoint(local, force:=True)
        Capture = True
        Cursor = Cursors.Cross
        Invalidate()
    End Sub

    ''' <summary>
    ''' Adds a sample to the active stroke. force=True always records (stroke start/end).
    ''' </summary>
    Private Sub AppendStrokePoint(local As Point, Optional force As Boolean = False)
        If _activeStroke Is Nothing Then Return
        Dim norm = DrawingHelper.ViewportToNormalized(local, Size, _pan, _zoom)
        If Not norm.HasValue Then Return

        Dim p = DrawingHelper.ClampNormalized(norm.Value)
        If _activeStroke.Points.Count = 0 OrElse force Then
            _activeStroke.Points.Add(p)
            Return
        End If

        Dim last = _activeStroke.Points(_activeStroke.Points.Count - 1)
        Dim dx = p.X - last.X
        Dim dy = p.Y - last.Y
        ' Keep early samples denser for a clean start; thin out later
        Dim minDistSq = If(_activeStroke.Points.Count < 4, 0.00000005F, 0.0000002F)
        If (dx * dx + dy * dy) >= minDistSq Then
            _activeStroke.Points.Add(p)
        End If
    End Sub

    Protected Overrides Sub OnMouseMove(e As MouseEventArgs)
        MyBase.OnMouseMove(e)

        If _mode = InteractMode.None Then
            Cursor = CursorForIdle(e.Location)
            Return
        End If

        Dim screenCursor = PointToScreen(e.Location)

        If _mode = InteractMode.Draw Then
            Dim before = If(_activeStroke IsNot Nothing, _activeStroke.Points.Count, 0)
            AppendStrokePoint(e.Location, force:=False)
            If _activeStroke IsNot Nothing AndAlso _activeStroke.Points.Count <> before Then
                Invalidate()
            End If
            Return
        End If

        If _mode = InteractMode.Move Then
            Dim parentCtrl = Parent
            If parentCtrl Is Nothing Then Return
            Dim newLoc = parentCtrl.PointToClient(screenCursor)
            newLoc.Offset(-_dragOffset.X, -_dragOffset.Y)
            newLoc.X = Math.Max(-Width + 40, newLoc.X)
            newLoc.Y = Math.Max(-Height + 40, newLoc.Y)
            If newLoc <> Location Then
                SetBounds(newLoc.X, newLoc.Y, Width, Height, BoundsSpecified.Location)
            End If

        ElseIf _mode = InteractMode.Pan Then
            Dim dx = screenCursor.X - _panStartCursor.X
            Dim dy = screenCursor.Y - _panStartCursor.Y
            Dim content = ZoomHelper.ContentSize(Size, _zoom)
            _pan = ZoomHelper.ClampPan(New Point(_panStart.X + dx, _panStart.Y + dy), content, Size)
            Invalidate()

        ElseIf _mode = InteractMode.Resize Then
            Dim dx = screenCursor.X - _resizeStartCursor.X
            Dim dy = screenCursor.Y - _resizeStartCursor.Y
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

    Protected Overrides Sub OnMouseWheel(e As MouseEventArgs)
        If (Control.ModifierKeys And Keys.Control) = Keys.Control Then
            ' Ctrl+wheel → zoom inside this frame only
            HandleWheelZoom(e.Delta, PointToScreen(e.Location))
            MarkWheelHandled(e)
            Return
        End If

        ' Plain wheel over image → scroll the parent canvas (not zoom)
        ForwardVerticalScrollToCanvas(e.Delta)
        MarkWheelHandled(e)
    End Sub

    ''' <summary>
    ''' Ctrl+vertical wheel zooms. Plain vertical wheel scrolls the canvas.
    ''' Horizontal wheel pans image content when zoomed; otherwise scrolls canvas horizontally.
    ''' </summary>
    Protected Overrides Sub WndProc(ByRef m As Message)
        Const WM_MOUSEWHEEL As Integer = &H20A
        Const WM_MOUSEHWHEEL As Integer = &H20E

        If m.Msg = WM_MOUSEWHEEL Then
            Dim delta = WheelScrollHelper.DeltaFromWParam(m.WParam)
            If (Control.ModifierKeys And Keys.Control) = Keys.Control Then
                HandleWheelZoom(delta, Cursor.Position)
            Else
                ForwardVerticalScrollToCanvas(delta)
            End If
            m.Result = New IntPtr(1)
            Return
        End If

        If m.Msg = WM_MOUSEHWHEEL Then
            Dim delta = WheelScrollHelper.DeltaFromWParam(m.WParam)
            If _zoom > 1.001 Then
                HandleWheelPanHorizontal(delta)
            Else
                ForwardHorizontalScrollToCanvas(delta)
            End If
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

    Private Sub ForwardVerticalScrollToCanvas(wheelDelta As Integer)
        Dim canvas = TryCast(Parent, ScreenshotCanvas)
        If canvas IsNot Nothing Then
            canvas.ScrollFromWheel(wheelDelta, horizontal:=False)
        End If
    End Sub

    Private Sub ForwardHorizontalScrollToCanvas(wheelDelta As Integer)
        Dim canvas = TryCast(Parent, ScreenshotCanvas)
        If canvas IsNot Nothing Then
            canvas.ScrollFromWheel(wheelDelta, horizontal:=True)
        End If
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
        Return MyBase.IsInputKey(keyData)
    End Function

    Protected Overrides Sub OnKeyDown(e As KeyEventArgs)
        MyBase.OnKeyDown(e)
        If e.KeyCode = Keys.Delete OrElse e.KeyCode = Keys.Back Then
            Dim canvas = TryCast(Parent, ScreenshotCanvas)
            If canvas IsNot Nothing AndAlso canvas.RemoveSelectedScreenshot() Then
                e.Handled = True
                e.SuppressKeyPress = True
            End If
        End If
    End Sub

    Protected Overrides Sub OnResize(e As EventArgs)
        MyBase.OnResize(e)
        Dim content = ZoomHelper.ContentSize(Size, _zoom)
        _pan = ZoomHelper.ClampPan(_pan, content, Size)
    End Sub

    Private Sub EndInteract()
        If _mode = InteractMode.None Then Return
        Dim wasMove = _mode = InteractMode.Move
        Dim wasResize = _mode = InteractMode.Resize
        Dim wasDraw = _mode = InteractMode.Draw
        _mode = InteractMode.None
        _resizeEdge = ResizeEdge.None
        Capture = False

        If wasDraw AndAlso _activeStroke IsNot Nothing Then
            ' Snap final point to cursor so the stroke end is flush (not short of release)
            AppendStrokePoint(PointToClient(Cursor.Position), force:=True)
            If _activeStroke.Points.Count >= 1 Then
                _strokes.Add(_activeStroke)
            End If
            _activeStroke = Nothing
            Invalidate()
        End If

        If wasMove Then
            RaisePosition()
        End If
        If wasResize Then
            RaiseTransform()
        End If
        Cursor = CursorForIdle()
        RaiseEvent InteractionEnded(Me, EventArgs.Empty)
    End Sub

    Private Function CursorForIdle(Optional local As Point? = Nothing) As Cursor
        If local.HasValue Then
            Dim edge = HitTestEdge(local.Value)
            If edge <> ResizeEdge.None Then Return CursorForEdge(edge)
        End If
        If _canvas IsNot Nothing AndAlso DrawingHelper.IsInkTool(_canvas.ActiveTool) Then
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
