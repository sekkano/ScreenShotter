''' <summary>
''' Per-tab canvas hosting movable/resizable/zoomable screenshots at full native frame size.
''' </summary>
Public Class ScreenshotCanvas
    Inherits Panel

    Private ReadOnly _session As TabSession
    Private ReadOnly _images As New Dictionary(Of Guid, Image)()
    Private ReadOnly _boxes As New Dictionary(Of Guid, MovableScreenshotBox)()
    Private _selectedId As Guid = Guid.Empty
    Private _scrollUpdatePending As Boolean

    Public Sub New(session As TabSession)
        _session = session
        Dock = DockStyle.Fill
        AutoScroll = True
        BackColor = Color.FromArgb(245, 245, 248)
        BorderStyle = BorderStyle.None

        ' Strong double-buffering to reduce ghost trails while dragging children
        SetStyle(ControlStyles.AllPaintingInWmPaint Or
                 ControlStyles.OptimizedDoubleBuffer Or
                 ControlStyles.UserPaint Or
                 ControlStyles.ResizeRedraw Or
                 ControlStyles.Selectable, True)
        UpdateStyles()
        DoubleBuffered = True
    End Sub

    Protected Overrides ReadOnly Property CreateParams As CreateParams
        Get
            Dim cp = MyBase.CreateParams
            ' WS_EX_COMPOSITED — paint children as a unit; reduces drag ghost trails
            cp.ExStyle = cp.ExStyle Or &H2000000
            Return cp
        End Get
    End Property

    Public ReadOnly Property Session As TabSession
        Get
            Return _session
        End Get
    End Property

    Public ReadOnly Property SelectedBox As MovableScreenshotBox
        Get
            If _selectedId = Guid.Empty Then Return Nothing
            Dim box As MovableScreenshotBox = Nothing
            If _boxes.TryGetValue(_selectedId, box) Then Return box
            Return Nothing
        End Get
    End Property

    Public Event SelectionChanged As EventHandler
    Public Event TransformChanged As EventHandler(Of TransformChangedEventArgs)

    ''' <summary>
    ''' Adds a screenshot at full native pixel frame size (zoom 100% inside). Takes ownership of image.
    ''' </summary>
    Public Function AddScreenshotImage(image As Image, Optional location As Point? = Nothing) As ScreenshotItem
        If image Is Nothing Then Throw New ArgumentNullException(NameOf(image))

        Dim loc = If(location, ComputeCascadeLocation())
        Dim size = image.Size
        Dim item = _session.AddScreenshot(loc, size)

        _images(item.Id) = image

        Dim box As New MovableScreenshotBox(item.Id, image) With {
            .Location = loc
        }
        AddHandler box.PositionChanged, AddressOf OnBoxPositionChanged
        AddHandler box.TransformChanged, AddressOf OnBoxTransformChanged
        AddHandler box.SelectedChanged, AddressOf OnBoxSelectedChanged
        AddHandler box.InteractionEnded, AddressOf OnBoxInteractionEnded

        _boxes(item.Id) = box
        SuspendLayout()
        Controls.Add(box)
        ResumeLayout(False)
        box.BringToFront()
        SelectBox(box)
        UpdateScrollBounds()
        Return item
    End Function

    Public Sub ZoomSelectedIn()
        SelectedBox?.ZoomIn()
    End Sub

    Public Sub ZoomSelectedOut()
        SelectedBox?.ZoomOut()
    End Sub

    Public Sub ZoomSelectedReset()
        SelectedBox?.ZoomReset()
    End Sub

    Private Function ComputeCascadeLocation() As Point
        Const offset = 24
        Dim n = _session.Items.Count
        Dim origin = New Point(-AutoScrollPosition.X, -AutoScrollPosition.Y)
        Return New Point(origin.X + 20 + (n * offset) Mod 200, origin.Y + 20 + (n * offset) Mod 160)
    End Function

    Private Sub OnBoxPositionChanged(sender As Object, e As PositionChangedEventArgs)
        _session.MoveScreenshot(e.ItemId, e.Location)
        ' Heavy scroll-size work only when not mid-drag (InteractionEnded handles that)
        Dim box = TryCast(sender, MovableScreenshotBox)
        If box Is Nothing OrElse Not box.IsInteracting Then
            UpdateScrollBounds()
        Else
            _scrollUpdatePending = True
        End If
    End Sub

    Private Sub OnBoxTransformChanged(sender As Object, e As TransformChangedEventArgs)
        Dim item = _session.TryGetItem(e.ItemId)
        If item IsNot Nothing Then
            item.Location = e.Location
            item.Size = e.Size
        End If
        Dim box = TryCast(sender, MovableScreenshotBox)
        If box Is Nothing OrElse Not box.IsInteracting Then
            UpdateScrollBounds()
        Else
            _scrollUpdatePending = True
        End If
        RaiseEvent TransformChanged(Me, e)
    End Sub

    Private Sub OnBoxSelectedChanged(sender As Object, e As EventArgs)
        Dim box = TryCast(sender, MovableScreenshotBox)
        If box Is Nothing Then Return
        SelectBox(box)
    End Sub

    Private Sub OnBoxInteractionEnded(sender As Object, e As EventArgs)
        If _scrollUpdatePending Then
            _scrollUpdatePending = False
            UpdateScrollBounds()
        Else
            UpdateScrollBounds()
        End If
        ' Sync model location after deferred move
        Dim box = TryCast(sender, MovableScreenshotBox)
        If box IsNot Nothing Then
            _session.MoveScreenshot(box.ItemId, box.Location)
            Dim item = _session.TryGetItem(box.ItemId)
            If item IsNot Nothing Then
                item.Size = box.Size
            End If
        End If
    End Sub

    Private Sub SelectBox(box As MovableScreenshotBox)
        If box Is Nothing Then Return
        _selectedId = box.ItemId
        For Each kvp In _boxes
            kvp.Value.Selected = (kvp.Key = _selectedId)
        Next
        box.BringToFront()
        RaiseEvent SelectionChanged(Me, EventArgs.Empty)
    End Sub

    Protected Overrides Sub OnMouseDown(e As MouseEventArgs)
        MyBase.OnMouseDown(e)
        If e.Button = MouseButtons.Left Then
            Focus()
        End If
    End Sub

    ''' <summary>
    ''' Plain wheel scrolls the canvas. Ctrl+wheel over a screenshot zooms that image.
    ''' Shift+wheel scrolls horizontally.
    ''' </summary>
    Protected Overrides Sub OnMouseWheel(e As MouseEventArgs)
        Dim ctrl = (Control.ModifierKeys And Keys.Control) = Keys.Control
        Dim box = If(ctrl, GetScreenshotUnderPointer(), Nothing)

        If box IsNot Nothing Then
            box.HandleWheelZoom(e.Delta, Cursor.Position)
            Dim handled = TryCast(e, HandledMouseEventArgs)
            If handled IsNot Nothing Then handled.Handled = True
            Return
        End If

        If (Control.ModifierKeys And Keys.Shift) = Keys.Shift Then
            ScrollFromWheel(e.Delta, horizontal:=True)
            Dim handled = TryCast(e, HandledMouseEventArgs)
            If handled IsNot Nothing Then handled.Handled = True
            Return
        End If

        MyBase.OnMouseWheel(e)
    End Sub

    ''' <summary>
    ''' Side-tilt wheel → horizontal AutoScroll. Ctrl+vertical over image is handled above.
    ''' </summary>
    Protected Overrides Sub WndProc(ByRef m As Message)
        Const WM_MOUSEWHEEL As Integer = &H20A
        Const WM_MOUSEHWHEEL As Integer = &H20E

        If m.Msg = WM_MOUSEWHEEL Then
            Dim ctrl = (Control.ModifierKeys And Keys.Control) = Keys.Control
            If ctrl Then
                Dim box = GetScreenshotUnderPointer()
                If box IsNot Nothing Then
                    Dim delta = WheelScrollHelper.DeltaFromWParam(m.WParam)
                    box.HandleWheelZoom(delta, Cursor.Position)
                    m.Result = New IntPtr(1)
                    Return
                End If
            End If
            ' Plain wheel: default AutoScroll vertical behavior
        End If

        If m.Msg = WM_MOUSEHWHEEL Then
            Dim delta = WheelScrollHelper.DeltaFromWParam(m.WParam)
            Dim box = GetScreenshotUnderPointer()
            If box IsNot Nothing AndAlso box.Zoom > 1.001 Then
                box.HandleWheelPanHorizontal(delta)
            Else
                ScrollFromWheel(delta, horizontal:=True)
            End If
            m.Result = New IntPtr(1)
            Return
        End If

        MyBase.WndProc(m)
    End Sub

    ''' <summary>
    ''' Scroll this canvas from a wheel delta (used when the pointer is over an image
    ''' but Ctrl is not held, so zoom should not happen).
    ''' </summary>
    Public Sub ScrollFromWheel(wheelDelta As Integer, horizontal As Boolean)
        Dim pixels = WheelScrollHelper.DeltaToScrollPixels(wheelDelta)
        ' Vertical wheel: positive delta = scroll up (decrease offset) — match WinForms AutoScroll
        If horizontal Then
            ApplyCanvasScroll(pixels, 0)
        Else
            ApplyCanvasScroll(0, -pixels)
        End If
    End Sub

    Private Function GetScreenshotUnderPointer() As MovableScreenshotBox
        Dim under = GetChildAtPoint(PointToClient(Cursor.Position), GetChildAtPointSkip.Invisible)
        Dim c = under
        While c IsNot Nothing
            Dim box = TryCast(c, MovableScreenshotBox)
            If box IsNot Nothing Then Return box
            c = c.Parent
            If c Is Me Then Exit While
        End While
        Return Nothing
    End Function

    Private Sub ApplyCanvasScroll(deltaXPixels As Integer, deltaYPixels As Integer)
        If deltaXPixels = 0 AndAlso deltaYPixels = 0 Then Return
        Dim nextPos = WheelScrollHelper.NextScrollPosition(AutoScrollPosition, deltaXPixels, deltaYPixels)
        AutoScrollPosition = nextPos
    End Sub

    Public Sub UpdateScrollBounds()
        Dim maxR = 0
        Dim maxB = 0
        For Each box In _boxes.Values
            maxR = Math.Max(maxR, box.Left + box.Width)
            maxB = Math.Max(maxB, box.Top + box.Height)
        Next
        Dim pad = 80
        Dim need = New Size(Math.Max(ClientSize.Width, maxR + pad), Math.Max(ClientSize.Height, maxB + pad))
        If AutoScrollMinSize <> need Then
            AutoScrollMinSize = need
        End If
    End Sub

    Protected Overrides Sub OnResize(eventargs As EventArgs)
        MyBase.OnResize(eventargs)
        UpdateScrollBounds()
    End Sub

    Protected Overrides Sub Dispose(disposing As Boolean)
        If disposing Then
            For Each box In _boxes.Values
                RemoveHandler box.PositionChanged, AddressOf OnBoxPositionChanged
                RemoveHandler box.TransformChanged, AddressOf OnBoxTransformChanged
                RemoveHandler box.SelectedChanged, AddressOf OnBoxSelectedChanged
                RemoveHandler box.InteractionEnded, AddressOf OnBoxInteractionEnded
            Next
            _boxes.Clear()
            For Each img In _images.Values
                img.Dispose()
            Next
            _images.Clear()
        End If
        MyBase.Dispose(disposing)
    End Sub
End Class
