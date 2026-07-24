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

    Public Sub New(itemId As Guid, image As Image)
        If image Is Nothing Then Throw New ArgumentNullException(NameOf(image))

        _itemId = itemId
        _image = image
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

    Protected Overrides Sub OnPaintBackground(e As PaintEventArgs)
        ' Fully painted in OnPaint — avoids flicker/ghost trails
    End Sub

    Protected Overrides Sub OnMouseDown(e As MouseEventArgs)
        MyBase.OnMouseDown(e)
        If e.Button = MouseButtons.Left Then
            SelectMe()
            BringToFront()
            Dim edge = HitTestEdge(e.Location)
            If edge <> ResizeEdge.None Then
                _mode = InteractMode.Resize
                _resizeEdge = edge
                _resizeStartCursor = PointToScreen(e.Location)
                _resizeStartLocation = Location
                _resizeStartSize = Size
                ' Aspect lock uses current frame size as reference (diagonal only)
                _aspectReference = Size
                Capture = True
            ElseIf (Control.ModifierKeys And Keys.Shift) = Keys.Shift OrElse _zoom > 1.001 Then
                ' Shift+drag or zoomed: pan content inside frame; plain drag moves frame when zoom≈1
                If (Control.ModifierKeys And Keys.Shift) = Keys.Shift Then
                    _mode = InteractMode.Pan
                    _panStartCursor = PointToScreen(e.Location)
                    _panStart = _pan
                    Capture = True
                Else
                    BeginMove(e.Location)
                End If
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

    Protected Overrides Sub OnMouseMove(e As MouseEventArgs)
        MyBase.OnMouseMove(e)

        If _mode = InteractMode.None Then
            Dim edge = HitTestEdge(e.Location)
            Cursor = CursorForEdge(edge)
            Return
        End If

        Dim screenCursor = PointToScreen(e.Location)

        If _mode = InteractMode.Move Then
            Dim parentCtrl = Parent
            If parentCtrl Is Nothing Then Return
            Dim newLoc = parentCtrl.PointToClient(screenCursor)
            newLoc.Offset(-_dragOffset.X, -_dragOffset.Y)
            newLoc.X = Math.Max(-Width + 40, newLoc.X)
            newLoc.Y = Math.Max(-Height + 40, newLoc.Y)
            If newLoc <> Location Then
                ' SetBoundsCore path — single move, no layout storm
                SetBounds(newLoc.X, newLoc.Y, Width, Height, BoundsSpecified.Location)
            End If
            ' Defer PositionChanged until mouse up (reduces ghosting from parent scroll work)

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
                ' Diagonal: preserve aspect of the frame at drag start
                display = ZoomHelper.AspectPreserveSize(_aspectReference, tentative)
                display = ZoomHelper.FreeResizeSize(display, MinDisplayPx)
            Else
                ' Edges: free stretch (independent width/height)
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
            ' Keep zoom; re-clamp pan to new viewport
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
        _mode = InteractMode.None
        _resizeEdge = ResizeEdge.None
        Capture = False

        If wasMove Then
            RaisePosition()
        End If
        If wasResize Then
            RaiseTransform()
        End If
        RaiseEvent InteractionEnded(Me, EventArgs.Empty)
    End Sub

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
