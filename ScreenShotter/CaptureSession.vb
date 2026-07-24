''' <summary>
''' Shared state for a multi-monitor capture: one overlay per screen, virtual-screen coordinates.
''' </summary>
Public Class CaptureSession
    Private ReadOnly _overlays As New List(Of CaptureOverlayForm)()
    Private _dragStartVirtual As Point
    Private _dragCurrentVirtual As Point
    Private _isDragging As Boolean
    Private _cursorVirtual As Point
    Private _hasCursor As Boolean
    Private _completed As Boolean
    Private _result As Bitmap
    Private _accepted As Boolean

    Public ReadOnly Property IsDragging As Boolean
        Get
            Return _isDragging
        End Get
    End Property

    Public ReadOnly Property IsCompleted As Boolean
        Get
            Return _completed
        End Get
    End Property

    Public ReadOnly Property Accepted As Boolean
        Get
            Return _accepted
        End Get
    End Property

    Public ReadOnly Property CursorVirtual As Point
        Get
            Return _cursorVirtual
        End Get
    End Property

    Public ReadOnly Property HasCursor As Boolean
        Get
            Return _hasCursor
        End Get
    End Property

    ''' <summary>
    ''' Current selection in virtual-screen coordinates (empty when not dragging / no selection).
    ''' </summary>
    Public ReadOnly Property SelectionVirtual As Rectangle
        Get
            If Not _isDragging AndAlso Not _accepted Then
                Return Rectangle.Empty
            End If
            Return RegionHelper.NormalizeRect(_dragStartVirtual, _dragCurrentVirtual)
        End Get
    End Property

    Public Sub Register(overlay As CaptureOverlayForm)
        If overlay Is Nothing Then Throw New ArgumentNullException(NameOf(overlay))
        _overlays.Add(overlay)
    End Sub

    Public Sub BeginDrag(virtualPoint As Point)
        _isDragging = True
        _dragStartVirtual = virtualPoint
        _dragCurrentVirtual = virtualPoint
        _cursorVirtual = virtualPoint
        _hasCursor = True
        InvalidateAllOverlays()
    End Sub

    Public Sub UpdateDrag(virtualPoint As Point)
        Dim oldSel = SelectionVirtual
        Dim oldCursor = _cursorVirtual
        _dragCurrentVirtual = virtualPoint
        _cursorVirtual = virtualPoint
        _hasCursor = True
        Dim newSel = SelectionVirtual
        InvalidateSelectionAndCursor(oldSel, newSel, oldCursor, virtualPoint)
    End Sub

    Public Sub UpdateCursor(virtualPoint As Point)
        If _isDragging Then
            UpdateDrag(virtualPoint)
            Return
        End If
        Dim oldCursor = _cursorVirtual
        Dim had = _hasCursor
        _cursorVirtual = virtualPoint
        _hasCursor = True
        If had Then
            InvalidateCursor(oldCursor, virtualPoint)
        Else
            InvalidateCursor(virtualPoint, virtualPoint)
        End If
    End Sub

    Public Sub CompleteDrag(virtualPoint As Point)
        If Not _isDragging Then Return
        _dragCurrentVirtual = virtualPoint
        _cursorVirtual = virtualPoint
        Dim region = RegionHelper.NormalizeRect(_dragStartVirtual, _dragCurrentVirtual)
        _isDragging = False
        If RegionHelper.IsValidCaptureRegion(region) Then
            FinishSuccess(region)
        Else
            FinishCancel()
        End If
    End Sub

    Public Sub Cancel()
        If _completed Then Return
        FinishCancel()
    End Sub

    Public Function TakeResult() As Bitmap
        Dim img = _result
        _result = Nothing
        Return img
    End Function

    Private Sub FinishSuccess(regionVirtual As Rectangle)
        Try
            _result = ComposeCapture(regionVirtual)
            _accepted = _result IsNot Nothing
        Catch
            _result?.Dispose()
            _result = Nothing
            _accepted = False
        End Try
        CompleteAndClose()
    End Sub

    Private Sub FinishCancel()
        _accepted = False
        _result?.Dispose()
        _result = Nothing
        CompleteAndClose()
    End Sub

    Private Sub CompleteAndClose()
        _completed = True
        _isDragging = False
        ' Close all overlays (copy list — Close may mutate)
        For Each overlay In _overlays.ToArray()
            Try
                overlay.Close()
            Catch
            End Try
        Next
    End Sub

    ''' <summary>
    ''' Stitches the selection from each monitor's pre-captured bitmap (handles any monitor count).
    ''' </summary>
    Private Function ComposeCapture(selectionVirtual As Rectangle) As Bitmap
        If Not RegionHelper.IsValidCaptureRegion(selectionVirtual) Then Return Nothing

        Dim result As New Bitmap(selectionVirtual.Width, selectionVirtual.Height)
        Using g = Graphics.FromImage(result)
            g.CompositingMode = Drawing2D.CompositingMode.SourceCopy
            g.Clear(Color.Black)
            For Each overlay In _overlays
                Dim mon = overlay.MonitorBounds
                Dim inter = MonitorCaptureHelper.IntersectVirtual(selectionVirtual, mon)
                If inter.Width <= 0 OrElse inter.Height <= 0 Then Continue For

                Dim src = MonitorCaptureHelper.SourceInMonitorBitmap(mon, inter)
                Dim dst = MonitorCaptureHelper.DestinationInCapture(selectionVirtual, inter)
                Dim bmp = overlay.MonitorBitmap
                If bmp Is Nothing Then Continue For
                g.DrawImage(bmp, dst, src, GraphicsUnit.Pixel)
            Next
        End Using
        Return result
    End Function

    Private Sub InvalidateAllOverlays()
        For Each overlay In _overlays
            overlay.InvalidateAllFast()
        Next
    End Sub

    Private Sub InvalidateSelectionAndCursor(oldSel As Rectangle, newSel As Rectangle, oldCursor As Point, newCursor As Point)
        For Each overlay In _overlays
            overlay.InvalidateVirtualRegions(oldSel, newSel, oldCursor, newCursor, includeCrosshair:=True)
        Next
    End Sub

    Private Sub InvalidateCursor(oldCursor As Point, newCursor As Point)
        For Each overlay In _overlays
            overlay.InvalidateVirtualRegions(Rectangle.Empty, Rectangle.Empty, oldCursor, newCursor, includeCrosshair:=True)
        Next
    End Sub

    ''' <summary>
    ''' Runs a capture across all monitors (or a filtered subset). Blocks until accept/cancel.
    ''' </summary>
    Public Shared Function Run(Optional monitors As IReadOnlyList(Of Screen) = Nothing) As CaptureSession
        Dim session As New CaptureSession()
        Dim screens = If(monitors IsNot Nothing AndAlso monitors.Count > 0,
            monitors,
            Screen.AllScreens.Cast(Of Screen)().ToList())

        If screens.Count = 0 Then
            session._completed = True
            session._accepted = False
            Return session
        End If

        Dim forms As New List(Of CaptureOverlayForm)()
        For Each scr In screens
            Dim form As New CaptureOverlayForm(scr, session)
            session.Register(form)
            forms.Add(form)
        Next

        For Each form In forms
            form.Show()
            form.Cursor = Cursors.Cross
        Next

        ' Activate the overlay under the cursor if possible
        Dim cursorPos = System.Windows.Forms.Cursor.Position
        For Each form In forms
            If form.MonitorBounds.Contains(cursorPos) Then
                form.Activate()
                Exit For
            End If
        Next

        While Not session.IsCompleted
            Application.DoEvents()
            Threading.Thread.Sleep(1)
        End While

        For Each form In forms
            Try
                form.Dispose()
            Catch
            End Try
        Next

        Return session
    End Function
End Class
