''' <summary>
''' Per-tab canvas hosting movable/resizable/zoomable screenshots.
''' Uses an inner surface panel so AutoScroll does not fight child Location
''' (avoids screenshots jumping when selecting/focusing).
''' </summary>
Public Class ScreenshotCanvas
    Inherits Panel

    Private ReadOnly _session As TabSession
    Private ReadOnly _images As New Dictionary(Of Guid, Image)()
    Private ReadOnly _boxes As New Dictionary(Of Guid, MovableScreenshotBox)()
    Private ReadOnly _surface As Panel
    Private ReadOnly _history As New UndoRedoStack(50)
    Private _selectedId As Guid = Guid.Empty
    Private _scrollUpdatePending As Boolean
    Private _activeTool As DrawingTool = DrawingTool.Pointer
    Private ReadOnly _drawingSettings As New DrawingSettings()

    Public Sub New(session As TabSession)
        _session = session
        Dock = DockStyle.Fill
        AutoScroll = True
        BackColor = Color.FromArgb(245, 245, 248)
        BorderStyle = BorderStyle.None

        SetStyle(ControlStyles.AllPaintingInWmPaint Or
                 ControlStyles.OptimizedDoubleBuffer Or
                 ControlStyles.UserPaint Or
                 ControlStyles.ResizeRedraw Or
                 ControlStyles.Selectable, True)
        UpdateStyles()
        DoubleBuffered = True

        ' Inner surface: children use simple (0,0) coordinates; this panel is the
        ' only AutoScroll child so focusing a screenshot does not re-home locations.
        _surface = New Panel() With {
            .Location = Point.Empty,
            .Size = New Size(1, 1),
            .BackColor = Color.FromArgb(245, 245, 248),
            .Margin = Padding.Empty,
            .Padding = Padding.Empty
        }
        Controls.Add(_surface)
    End Sub

    ''' <summary>Host for screenshot controls (document coordinate space).</summary>
    Public ReadOnly Property Surface As Panel
        Get
            Return _surface
        End Get
    End Property

    <System.ComponentModel.Browsable(False)>
    <System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)>
    Public Property ActiveTool As DrawingTool
        Get
            Return _activeTool
        End Get
        Set(value As DrawingTool)
            If _activeTool = value Then Return
            _activeTool = value
            If DrawingHelper.IsInkTool(value) Then
                _drawingSettings.Tool = value
            End If
            For Each box In _boxes.Values
                box.NotifyToolChanged()
            Next
        End Set
    End Property

    <System.ComponentModel.Browsable(False)>
    <System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)>
    Public ReadOnly Property DrawingSettings As DrawingSettings
        Get
            Return _drawingSettings
        End Get
    End Property

    Public Sub ApplyDrawingSettings(tool As DrawingTool, settings As DrawingSettings)
        If settings IsNot Nothing Then
            _drawingSettings.Tool = settings.Tool
            _drawingSettings.BaseColor = settings.BaseColor
            _drawingSettings.OpacityPercent = settings.OpacityPercent
            _drawingSettings.Thickness = settings.Thickness
        End If
        ActiveTool = tool
    End Sub

    ''' <summary>
    ''' Never auto-scroll when a child is focused — that jumps the viewport.
    ''' </summary>
    Protected Overrides Function ScrollToControl(activeControl As Control) As Point
        Return DisplayRectangle.Location
    End Function

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

    Public Function AddScreenshotImage(image As Image, Optional location As Point? = Nothing, Optional recordHistory As Boolean = True) As ScreenshotItem
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
        _surface.SuspendLayout()
        _surface.Controls.Add(box)
        _surface.ResumeLayout(False)
        box.BringToFront()
        SelectBox(box)
        UpdateScrollBounds()
        If recordHistory Then
            _history.Push(New AddScreenshotHistoryAction(Me, item.Id))
        End If
        Return item
    End Function

    Public Function FindBox(itemId As Guid) As MovableScreenshotBox
        Dim box As MovableScreenshotBox = Nothing
        If _boxes.TryGetValue(itemId, box) Then Return box
        Return Nothing
    End Function

    Public Sub SyncBoxModel(itemId As Guid)
        Dim box = FindBox(itemId)
        If box Is Nothing Then Return
        _session.MoveScreenshot(itemId, box.Location)
        Dim item = _session.TryGetItem(itemId)
        If item IsNot Nothing Then
            item.Location = box.Location
            item.Size = box.Size
        End If
    End Sub

    Public Sub RecordStroke(itemId As Guid, stroke As InkStroke)
        If stroke Is Nothing Then Return
        _history.Push(New StrokeHistoryAction(Me, itemId, stroke))
    End Sub

    Public Sub RecordAnnotationAdded(itemId As Guid, annotation As AnnotationBase)
        If annotation Is Nothing Then Return
        _history.Push(New AnnotationAddHistoryAction(Me, itemId, annotation))
    End Sub

    Public Sub RecordAnnotationRemoved(itemId As Guid, annotation As AnnotationBase)
        If annotation Is Nothing Then Return
        _history.Push(New AnnotationRemoveHistoryAction(Me, itemId, annotation))
    End Sub

    Public Sub RecordAnnotationChanged(itemId As Guid, before As AnnotationBase, after As AnnotationBase)
        If before Is Nothing OrElse after Is Nothing Then Return
        _history.Push(New AnnotationEditHistoryAction(Me, itemId, before, after))
    End Sub

    Public Sub RecordTransform(itemId As Guid, before As BoxTransformState, after As BoxTransformState)
        If before.EqualsState(after) Then Return
        _history.Push(New TransformHistoryAction(Me, itemId, before, after))
    End Sub

    Public Function CanUndo() As Boolean
        Return _history.CanUndo
    End Function

    Public Function CanRedo() As Boolean
        Return _history.CanRedo
    End Function

    Public Function Undo() As Boolean
        Return _history.Undo()
    End Function

    Public Function Redo() As Boolean
        Return _history.Redo()
    End Function

    ''' <summary>
    ''' Clones image + transform + strokes for restore after delete / undoing an add.
    ''' </summary>
    Public Function TakeSnapshot(itemId As Guid) As ScreenshotSnapshot
        Dim box = FindBox(itemId)
        Dim img As Image = Nothing
        If box Is Nothing OrElse Not _images.TryGetValue(itemId, img) OrElse img Is Nothing Then
            Return Nothing
        End If
        Return New ScreenshotSnapshot With {
            .ItemId = itemId,
            .Image = DirectCast(img.Clone(), Image),
            .Transform = box.CaptureTransform(),
            .Strokes = box.CloneStrokes(),
            .Annotations = box.CloneAnnotations()
        }
    End Function

    ''' <summary>
    ''' Re-adds a screenshot from a history snapshot (same id, image clone, strokes).
    ''' </summary>
    Public Sub RestoreSnapshot(snapshot As ScreenshotSnapshot)
        If snapshot Is Nothing OrElse snapshot.Image Is Nothing Then Return
        If _boxes.ContainsKey(snapshot.ItemId) Then Return

        Dim owned = DirectCast(snapshot.Image.Clone(), Image)
        Dim loc = snapshot.Transform.Location
        Dim size = snapshot.Transform.Size
        If size.Width < 1 OrElse size.Height < 1 Then
            size = owned.Size
        End If

        Dim item = _session.AddScreenshot(loc, size, snapshot.ItemId)
        _images(item.Id) = owned

        Dim box As New MovableScreenshotBox(item.Id, owned, Me) With {
            .Location = loc
        }
        box.ApplyTransform(snapshot.Transform)
        box.ReplaceStrokes(snapshot.Strokes)
        box.ReplaceAnnotations(snapshot.Annotations)

        AddHandler box.PositionChanged, AddressOf OnBoxPositionChanged
        AddHandler box.TransformChanged, AddressOf OnBoxTransformChanged
        AddHandler box.SelectedChanged, AddressOf OnBoxSelectedChanged
        AddHandler box.InteractionEnded, AddressOf OnBoxInteractionEnded

        _boxes(item.Id) = box
        _surface.SuspendLayout()
        _surface.Controls.Add(box)
        _surface.ResumeLayout(False)
        box.BringToFront()
        SelectBox(box)
        SyncBoxModel(item.Id)
        UpdateScrollBounds()
    End Sub

    Public Sub ZoomSelectedIn()
        SelectedBox?.ZoomIn()
    End Sub

    Public Sub ZoomSelectedOut()
        SelectedBox?.ZoomOut()
    End Sub

    Public Sub ZoomSelectedReset()
        SelectedBox?.ZoomReset()
    End Sub

    Public Function RemoveSelectedScreenshot() As Boolean
        If _selectedId = Guid.Empty Then Return False
        Return RemoveScreenshot(_selectedId)
    End Function

    Public ReadOnly Property ScreenshotCount As Integer
        Get
            Return _boxes.Count
        End Get
    End Property

    Public Function RenderTabComposite() As Bitmap
        If _boxes.Count = 0 Then Return Nothing

        Dim ordered = New List(Of MovableScreenshotBox)()
        For Each idx In TabExportHelper.BottomToTopControlIndices(_surface.Controls.Count)
            Dim box = TryCast(_surface.Controls(idx), MovableScreenshotBox)
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

    Public Function SaveTabComposite(path As String) As Boolean
        If Not TabExportHelper.IsValidSavePath(path) Then Return False
        Using bmp = RenderTabComposite()
            If bmp Is Nothing Then Return False
            Dim format = TabExportHelper.FormatFromPath(path)
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

    Public Function RemoveScreenshot(id As Guid, Optional recordHistory As Boolean = True) As Boolean
        If id = Guid.Empty Then Return False

        Dim box As MovableScreenshotBox = Nothing
        If Not _boxes.TryGetValue(id, box) Then
            Return _session.RemoveScreenshot(id)
        End If

        Dim snapshot As ScreenshotSnapshot = Nothing
        If recordHistory Then
            snapshot = TakeSnapshot(id)
        End If

        RemoveHandler box.PositionChanged, AddressOf OnBoxPositionChanged
        RemoveHandler box.TransformChanged, AddressOf OnBoxTransformChanged
        RemoveHandler box.SelectedChanged, AddressOf OnBoxSelectedChanged
        RemoveHandler box.InteractionEnded, AddressOf OnBoxInteractionEnded

        _boxes.Remove(id)
        _surface.Controls.Remove(box)
        box.Dispose()

        Dim img As Image = Nothing
        If _images.TryGetValue(id, img) Then
            _images.Remove(id)
            img.Dispose()
        End If

        _session.RemoveScreenshot(id)

        If _selectedId = id Then
            _selectedId = Guid.Empty
            Dim nextBox = _boxes.Values.LastOrDefault()
            If nextBox IsNot Nothing Then
                SelectBox(nextBox)
            Else
                RaiseEvent SelectionChanged(Me, EventArgs.Empty)
            End If
        End If

        UpdateScrollBounds()
        If recordHistory AndAlso snapshot IsNot Nothing Then
            _history.Push(New DeleteScreenshotHistoryAction(Me, snapshot))
        End If
        Return True
    End Function

    Private Function ComputeNextPlacement(newSize As Size) As Point
        Dim frames As New List(Of Rectangle)()
        For Each item In _session.Items
            Dim box As MovableScreenshotBox = Nothing
            If _boxes.TryGetValue(item.Id, box) Then
                frames.Add(box.Bounds)
            Else
                frames.Add(New Rectangle(item.Location, item.Size))
            End If
        Next

        ' Document-space top-left of what is currently visible (accounts for AutoScroll)
        Dim viewOrigin As New Point(
            Math.Max(0, -AutoScrollPosition.X),
            Math.Max(0, -AutoScrollPosition.Y))

        Return ScreenshotLayoutHelper.PlaceNextScreenshot(
            frames,
            newSize,
            viewportOrigin:=viewOrigin,
            viewportWidth:=Math.Max(1, ClientSize.Width))
    End Function

    Private Sub OnBoxPositionChanged(sender As Object, e As PositionChangedEventArgs)
        _session.MoveScreenshot(e.ItemId, e.Location)
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
        _scrollUpdatePending = False
        Dim box = TryCast(sender, MovableScreenshotBox)
        If box IsNot Nothing Then
            _session.MoveScreenshot(box.ItemId, box.Location)
            Dim item = _session.TryGetItem(box.ItemId)
            If item IsNot Nothing Then
                item.Size = box.Size
                item.Location = box.Location
            End If
        End If
        UpdateScrollBounds()
    End Sub

    Public Sub CancelInteractionsExcept(except As MovableScreenshotBox)
        For Each box In _boxes.Values
            If except Is Nothing OrElse Not Object.ReferenceEquals(box, except) Then
                If box.IsInteracting Then
                    box.CancelInteraction()
                End If
            End If
        Next
    End Sub

    Private Sub SelectBox(box As MovableScreenshotBox)
        If box Is Nothing Then Return
        CancelInteractionsExcept(box)
        _selectedId = box.ItemId
        For Each kvp In _boxes
            kvp.Value.Selected = (kvp.Key = _selectedId)
        Next
        box.BringToFront()
        ' Never Focus a screenshot — that triggers scroll-into-view jumps.
        RaiseEvent SelectionChanged(Me, EventArgs.Empty)
    End Sub

    Protected Overrides Sub OnMouseDown(e As MouseEventArgs)
        MyBase.OnMouseDown(e)
        If e.Button = MouseButtons.Left Then
            Focus()
            CancelInteractionsExcept(Nothing)
        End If
    End Sub

    Protected Overrides Sub OnMouseUp(e As MouseEventArgs)
        MyBase.OnMouseUp(e)
        ' Clear select-pending on any box if mouse released outside it (no Capture yet)
        For Each box In _boxes.Values
            If box.IsInteracting Then
                box.NotifyGlobalMouseUp()
            End If
        Next
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

    Protected Overrides Sub OnMouseWheel(e As MouseEventArgs)
        Dim ctrl = (Control.ModifierKeys And Keys.Control) = Keys.Control
        Dim shift = (Control.ModifierKeys And Keys.Shift) = Keys.Shift
        Dim box = GetScreenshotUnderPointer()

        ' Shift + wheel → zoom hovered screenshot
        If shift AndAlso box IsNot Nothing Then
            box.HandleWheelZoom(e.Delta, Cursor.Position)
            Dim handledZ = TryCast(e, HandledMouseEventArgs)
            If handledZ IsNot Nothing Then handledZ.Handled = True
            Return
        End If

        ' Ctrl + wheel → pan zoomed image (else form scroll)
        If ctrl AndAlso box IsNot Nothing AndAlso box.HandleWheelPan(e.Delta, horizontal:=False) Then
            Dim handled = TryCast(e, HandledMouseEventArgs)
            If handled IsNot Nothing Then handled.Handled = True
            Return
        End If

        MyBase.OnMouseWheel(e)
    End Sub

    Protected Overrides Sub WndProc(ByRef m As Message)
        Const WM_MOUSEWHEEL As Integer = &H20A
        Const WM_MOUSEHWHEEL As Integer = &H20E

        If m.Msg = WM_MOUSEWHEEL Then
            Dim ctrl = (Control.ModifierKeys And Keys.Control) = Keys.Control
            Dim shift = (Control.ModifierKeys And Keys.Shift) = Keys.Shift
            Dim box = GetScreenshotUnderPointer()
            Dim delta = WheelScrollHelper.DeltaFromWParam(m.WParam)

            If shift AndAlso box IsNot Nothing Then
                box.HandleWheelZoom(delta, Cursor.Position)
                m.Result = New IntPtr(1)
                Return
            End If

            If ctrl AndAlso box IsNot Nothing Then
                If box.HandleWheelPan(delta, horizontal:=False) Then
                    m.Result = New IntPtr(1)
                    Return
                End If
            End If
            ' Default: vertical form AutoScroll
        End If

        If m.Msg = WM_MOUSEHWHEEL Then
            Dim delta = WheelScrollHelper.DeltaFromWParam(m.WParam)
            Dim ctrl = (Control.ModifierKeys And Keys.Control) = Keys.Control
            Dim box = GetScreenshotUnderPointer()
            If ctrl AndAlso box IsNot Nothing AndAlso box.HandleWheelPan(delta, horizontal:=True) Then
                m.Result = New IntPtr(1)
                Return
            End If
            ScrollFromWheel(delta, horizontal:=True)
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

    Public Sub ScrollFromWheel(wheelDelta As Integer, horizontal As Boolean)
        Dim pixels = WheelScrollHelper.DeltaToScrollPixels(wheelDelta)
        If horizontal Then
            ApplyCanvasScroll(pixels, 0)
        Else
            ApplyCanvasScroll(0, -pixels)
        End If
    End Sub

    Private Function GetScreenshotUnderPointer() As MovableScreenshotBox
        Dim pt = _surface.PointToClient(Cursor.Position)
        Dim under = _surface.GetChildAtPoint(pt, GetChildAtPointSkip.Invisible)
        Return TryCast(under, MovableScreenshotBox)
    End Function

    Private Sub ApplyCanvasScroll(deltaXPixels As Integer, deltaYPixels As Integer)
        If deltaXPixels = 0 AndAlso deltaYPixels = 0 Then Return
        Dim nextPos = WheelScrollHelper.NextScrollPosition(AutoScrollPosition, deltaXPixels, deltaYPixels)
        AutoScrollPosition = nextPos
    End Sub

    Public Sub UpdateScrollBounds()
        Dim frames As New List(Of Rectangle)(_boxes.Count)
        For Each box In _boxes.Values
            Dim clamped = CanvasCoordinateHelper.ClampDocumentLocation(box.Location, box.Size)
            If clamped <> box.Location Then
                box.Location = clamped
            End If
            frames.Add(box.Bounds)
        Next

        Dim need = CanvasCoordinateHelper.ComputeScrollMinSize(frames, ClientSize, pad:=120)
        If need.Width < 1 Then need = New Size(1, 1)
        If need.Height < 1 Then need = New Size(need.Width, 1)

        ' Grow the surface; AutoScroll uses the surface as its scrollable child.
        If _surface.Size <> need Then
            _surface.Size = need
        End If
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
            _history.Clear()
        End If
        MyBase.Dispose(disposing)
    End Sub
End Class
