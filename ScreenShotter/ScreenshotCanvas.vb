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
    Private _activeTool As DrawingTool = DrawingTool.Pointer

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

    ''' <summary>
    ''' Current annotation tool for this tab (Pointer = move/resize, Highlighter = ink).
    ''' </summary>
    <System.ComponentModel.Browsable(False)>
    <System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)>
    Public Property ActiveTool As DrawingTool
        Get
            Return _activeTool
        End Get
        Set(value As DrawingTool)
            If _activeTool = value Then Return
            _activeTool = value
            For Each box In _boxes.Values
                box.NotifyToolChanged()
            Next
        End Set
    End Property

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

        Dim size = image.Size
        Dim loc = If(location, ComputeNextPlacement(size))
        Dim item = _session.AddScreenshot(loc, size)

        _images(item.Id) = image

        Dim box As New MovableScreenshotBox(item.Id, image, Me) With {
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

    ''' <summary>
    ''' Removes the currently selected screenshot from this tab (model + UI + image).
    ''' Returns True if something was deleted.
    ''' </summary>
    Public Function RemoveSelectedScreenshot() As Boolean
        If _selectedId = Guid.Empty Then Return False
        Return RemoveScreenshot(_selectedId)
    End Function

    ''' <summary>
    ''' Number of screenshots currently on this tab.
    ''' </summary>
    Public ReadOnly Property ScreenshotCount As Integer
        Get
            Return _boxes.Count
        End Get
    End Property

    ''' <summary>
    ''' Composites every screenshot on this tab as currently displayed (positions, sizes,
    ''' zoom/pan, z-order). Caller owns and must dispose the returned bitmap.
    ''' Returns Nothing when the tab has no screenshots.
    ''' </summary>
    Public Function RenderTabComposite() As Bitmap
        If _boxes.Count = 0 Then Return Nothing

        ' WinForms: Controls(0) is front/top. Paint back→front so the top-most
        ' screenshot is drawn last and stays on top in the export (matches the app).
        Dim ordered = New List(Of MovableScreenshotBox)()
        For Each idx In TabExportHelper.BottomToTopControlIndices(Controls.Count)
            Dim box = TryCast(Controls(idx), MovableScreenshotBox)
            If box IsNot Nothing Then ordered.Add(box)
        Next
        If ordered.Count = 0 Then Return Nothing

        Dim frames = ordered.Select(Function(b) b.Bounds).ToList()
        Dim union = TabExportHelper.ComputeUnionBounds(frames)
        If union.Width <= 0 OrElse union.Height <= 0 Then Return Nothing

        Dim bmp As New Bitmap(union.Width, union.Height, Imaging.PixelFormat.Format32bppArgb)
        Using g = Graphics.FromImage(bmp)
            g.Clear(Color.Transparent)
            g.CompositingMode = Drawing2D.CompositingMode.SourceOver
            g.CompositingQuality = Drawing2D.CompositingQuality.HighQuality

            For Each box In ordered
                Dim dest = TabExportHelper.FrameInExport(union, box.Bounds)
                box.DrawContentAsDisplayed(g, dest)
            Next
        End Using
        Return bmp
    End Function

    ''' <summary>
    ''' Renders the current tab composite and saves it to <paramref name="path"/>.
    ''' Format is chosen from the file extension (PNG default).
    ''' </summary>
    Public Function SaveTabComposite(path As String) As Boolean
        If Not TabExportHelper.IsValidSavePath(path) Then Return False
        Using bmp = RenderTabComposite()
            If bmp Is Nothing Then Return False
            Dim format = TabExportHelper.FormatFromPath(path)
            ' JPEG does not support transparency — flatten onto white
            If format.Equals(Imaging.ImageFormat.Jpeg) Then
                Using flat As New Bitmap(bmp.Width, bmp.Height, Imaging.PixelFormat.Format24bppRgb)
                    Using g = Graphics.FromImage(flat)
                        g.Clear(Color.White)
                        g.DrawImageUnscaled(bmp, 0, 0)
                    End Using
                    flat.Save(path, format)
                End Using
            Else
                bmp.Save(path, format)
            End If
        End Using
        Return True
    End Function

    ''' <summary>
    ''' Removes a screenshot by id. Disposes the image and control.
    ''' </summary>
    Public Function RemoveScreenshot(id As Guid) As Boolean
        If id = Guid.Empty Then Return False

        Dim box As MovableScreenshotBox = Nothing
        If Not _boxes.TryGetValue(id, box) Then
            ' Still try model cleanup
            Return _session.RemoveScreenshot(id)
        End If

        RemoveHandler box.PositionChanged, AddressOf OnBoxPositionChanged
        RemoveHandler box.TransformChanged, AddressOf OnBoxTransformChanged
        RemoveHandler box.SelectedChanged, AddressOf OnBoxSelectedChanged
        RemoveHandler box.InteractionEnded, AddressOf OnBoxInteractionEnded

        _boxes.Remove(id)
        Controls.Remove(box)
        box.Dispose()

        Dim img As Image = Nothing
        If _images.TryGetValue(id, img) Then
            _images.Remove(id)
            img.Dispose()
        End If

        _session.RemoveScreenshot(id)

        If _selectedId = id Then
            _selectedId = Guid.Empty
            ' Select another remaining screenshot if any (top-most / last in z-order preferred)
            Dim nextBox = _boxes.Values.LastOrDefault()
            If nextBox IsNot Nothing Then
                SelectBox(nextBox)
            Else
                RaiseEvent SelectionChanged(Me, EventArgs.Empty)
            End If
        End If

        UpdateScrollBounds()
        Return True
    End Function

    ''' <summary>
    ''' Places a new screenshot to the right of the previous one, or on a new row below.
    ''' </summary>
    Private Function ComputeNextPlacement(newSize As Size) As Point
        Dim origin = New Point(-AutoScrollPosition.X, -AutoScrollPosition.Y)
        Dim frames As New List(Of Rectangle)()
        ' Use live control bounds (current size/position), in add order via session when possible
        For Each item In _session.Items
            Dim box As MovableScreenshotBox = Nothing
            If _boxes.TryGetValue(item.Id, box) Then
                frames.Add(box.Bounds)
            Else
                frames.Add(New Rectangle(item.Location, item.Size))
            End If
        Next
        Return ScreenshotLayoutHelper.PlaceNextScreenshot(frames, newSize, origin)
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
        If box.CanFocus Then box.Focus()
        RaiseEvent SelectionChanged(Me, EventArgs.Empty)
    End Sub

    Protected Overrides Sub OnMouseDown(e As MouseEventArgs)
        MyBase.OnMouseDown(e)
        If e.Button = MouseButtons.Left Then
            Focus()
        End If
    End Sub

    Protected Overrides Sub OnKeyDown(e As KeyEventArgs)
        MyBase.OnKeyDown(e)
        If e.KeyCode = Keys.Delete OrElse e.KeyCode = Keys.Back Then
            If RemoveSelectedScreenshot() Then
                e.Handled = True
                e.SuppressKeyPress = True
            End If
        End If
    End Sub

    Protected Overrides Function IsInputKey(keyData As Keys) As Boolean
        Dim key = keyData And Not Keys.Modifiers
        If key = Keys.Delete OrElse key = Keys.Back Then Return True
        Return MyBase.IsInputKey(keyData)
    End Function

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

        If m.Msg = MouseActivateHelper.WM_MOUSEACTIVATE Then
            MyBase.WndProc(m)
            m.Result = MouseActivateHelper.ActivateAndPassClick
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
