''' <summary>
''' Per-monitor dimmed overlay with fast dirty-region painting, crosshair, and drag selection.
''' One instance is created per screen so any number of monitors is covered.
''' </summary>
Public Class CaptureOverlayForm
    Inherits Form

    Private ReadOnly _session As CaptureSession
    Private ReadOnly _monitorBounds As Rectangle
    Private _monitorBitmap As Bitmap
    Private ReadOnly _dimBrush As New SolidBrush(Color.FromArgb(140, 0, 0, 0))
    Private ReadOnly _borderPen As New Pen(Color.FromArgb(255, 0, 120, 215), 2.0F)
    Private ReadOnly _crossPen As New Pen(Color.FromArgb(200, 255, 255, 255), 1.0F)

    Public ReadOnly Property MonitorBounds As Rectangle
        Get
            Return _monitorBounds
        End Get
    End Property

    Public ReadOnly Property MonitorBitmap As Bitmap
        Get
            Return _monitorBitmap
        End Get
    End Property

    Public Sub New(screen As Screen, session As CaptureSession)
        If screen Is Nothing Then Throw New ArgumentNullException(NameOf(screen))
        If session Is Nothing Then Throw New ArgumentNullException(NameOf(session))

        _session = session
        _monitorBounds = screen.Bounds

        FormBorderStyle = FormBorderStyle.None
        ShowInTaskbar = False
        TopMost = True
        KeyPreview = True
        StartPosition = FormStartPosition.Manual
        Location = _monitorBounds.Location
        Size = _monitorBounds.Size
        Cursor = Cursors.Cross
        BackColor = Color.Black

        SetStyle(ControlStyles.AllPaintingInWmPaint Or
                 ControlStyles.UserPaint Or
                 ControlStyles.OptimizedDoubleBuffer Or
                 ControlStyles.ResizeRedraw, True)
        UpdateStyles()

        CaptureMonitor()
    End Sub

    Private Sub CaptureMonitor()
        _monitorBitmap = New Bitmap(_monitorBounds.Width, _monitorBounds.Height, Imaging.PixelFormat.Format32bppPArgb)
        Using g = Graphics.FromImage(_monitorBitmap)
            g.CompositingMode = Drawing2D.CompositingMode.SourceCopy
            g.CopyFromScreen(_monitorBounds.Location, Point.Empty, _monitorBounds.Size)
        End Using
    End Sub

    Protected Overrides ReadOnly Property CreateParams As CreateParams
        Get
            Dim cp = MyBase.CreateParams
            ' WS_EX_TOOLWINDOW — hide from alt-tab; keep topmost layer snappy
            cp.ExStyle = cp.ExStyle Or &H80
            Return cp
        End Get
    End Property

    Protected Overrides Sub OnShown(e As EventArgs)
        MyBase.OnShown(e)
        Cursor = Cursors.Cross
        ' Avoid BackgroundImage path — we paint from the cached bitmap ourselves
        BackgroundImage = Nothing
    End Sub

    Protected Overrides Sub OnPaintBackground(e As PaintEventArgs)
        ' Fully handled in OnPaint for clip-aware blits
    End Sub

    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        Dim g = e.Graphics
        g.CompositingMode = Drawing2D.CompositingMode.SourceCopy
        g.CompositingQuality = Drawing2D.CompositingQuality.HighSpeed
        g.InterpolationMode = Drawing2D.InterpolationMode.NearestNeighbor
        g.SmoothingMode = Drawing2D.SmoothingMode.None
        g.PixelOffsetMode = Drawing2D.PixelOffsetMode.None

        Dim clip = e.ClipRectangle
        If clip.Width <= 0 OrElse clip.Height <= 0 Then Return

        ' 1) Desktop pixels for this dirty region only
        If _monitorBitmap IsNot Nothing Then
            g.DrawImage(_monitorBitmap, clip, clip, GraphicsUnit.Pixel)
        Else
            g.FillRectangle(Brushes.Black, clip)
        End If

        g.CompositingMode = Drawing2D.CompositingMode.SourceOver

        ' 2) Dim everything except the clear selection hole
        Dim selLocal = LocalSelection()
        If RegionHelper.IsValidCaptureRegion(selLocal) Then
            FillDimExceptSelection(g, clip, selLocal)
            ' Border (only if it intersects clip)
            Dim border As New Rectangle(selLocal.X, selLocal.Y, Math.Max(0, selLocal.Width - 1), Math.Max(0, selLocal.Height - 1))
            If clip.IntersectsWith(Rectangle.Inflate(border, 2, 2)) Then
                g.DrawRectangle(_borderPen, border)
            End If
        Else
            g.FillRectangle(_dimBrush, clip)
        End If

        ' 3) Crosshair at cursor (virtual → local)
        If _session.HasCursor Then
            Dim localCursor = MonitorCaptureHelper.ToLocalPoint(_monitorBounds, _session.CursorVirtual)
            DrawCrosshair(g, localCursor, clip, selLocal)
        End If
    End Sub

    Private Sub FillDimExceptSelection(g As Graphics, clip As Rectangle, selLocal As Rectangle)
        ' Four rectangles around the selection, intersected with clip for speed
        Dim top As New Rectangle(0, 0, ClientSize.Width, Math.Max(0, selLocal.Top))
        Dim left As New Rectangle(0, selLocal.Top, Math.Max(0, selLocal.Left), selLocal.Height)
        Dim right As New Rectangle(selLocal.Right, selLocal.Top, Math.Max(0, ClientSize.Width - selLocal.Right), selLocal.Height)
        Dim bottom As New Rectangle(0, selLocal.Bottom, ClientSize.Width, Math.Max(0, ClientSize.Height - selLocal.Bottom))

        FillClipped(g, top, clip)
        FillClipped(g, left, clip)
        FillClipped(g, right, clip)
        FillClipped(g, bottom, clip)
    End Sub

    Private Sub FillClipped(g As Graphics, area As Rectangle, clip As Rectangle)
        If area.Width <= 0 OrElse area.Height <= 0 Then Return
        Dim r = Rectangle.Intersect(area, clip)
        If r.Width > 0 AndAlso r.Height > 0 Then
            g.FillRectangle(_dimBrush, r)
        End If
    End Sub

    Private Sub DrawCrosshair(g As Graphics, localCursor As Point, clip As Rectangle, selLocal As Rectangle)
        ' Skip crosshair inside the clear selection hole while dragging (cleaner snip UX)
        Dim inHole = RegionHelper.IsValidCaptureRegion(selLocal) AndAlso selLocal.Contains(localCursor)

        Dim x = localCursor.X
        Dim y = localCursor.Y
        If x < 0 OrElse y < 0 OrElse x >= ClientSize.Width OrElse y >= ClientSize.Height Then
            ' Cursor is on another monitor — still draw full-span lines if selection spans here? skip
            Return
        End If

        If Not inHole Then
            If clip.Top <= y AndAlso y < clip.Bottom Then
                g.DrawLine(_crossPen, clip.Left, y, clip.Right, y)
            End If
            If clip.Left <= x AndAlso x < clip.Right Then
                g.DrawLine(_crossPen, x, clip.Top, x, clip.Bottom)
            End If
        Else
            ' Draw crosshair only in dimmed area (outside hole): four segments
            ' Horizontal left of hole
            If y >= selLocal.Top AndAlso y < selLocal.Bottom Then
                If clip.Top <= y AndAlso y < clip.Bottom Then
                    If selLocal.Left > 0 Then
                        g.DrawLine(_crossPen, Math.Max(clip.Left, 0), y, Math.Min(clip.Right, selLocal.Left), y)
                    End If
                    If selLocal.Right < ClientSize.Width Then
                        g.DrawLine(_crossPen, Math.Max(clip.Left, selLocal.Right), y, Math.Min(clip.Right, ClientSize.Width), y)
                    End If
                End If
            ElseIf clip.Top <= y AndAlso y < clip.Bottom Then
                g.DrawLine(_crossPen, clip.Left, y, clip.Right, y)
            End If
            ' Vertical above/below hole
            If x >= selLocal.Left AndAlso x < selLocal.Right Then
                If clip.Left <= x AndAlso x < clip.Right Then
                    If selLocal.Top > 0 Then
                        g.DrawLine(_crossPen, x, Math.Max(clip.Top, 0), x, Math.Min(clip.Bottom, selLocal.Top))
                    End If
                    If selLocal.Bottom < ClientSize.Height Then
                        g.DrawLine(_crossPen, x, Math.Max(clip.Top, selLocal.Bottom), x, Math.Min(clip.Bottom, ClientSize.Height))
                    End If
                End If
            ElseIf clip.Left <= x AndAlso x < clip.Right Then
                g.DrawLine(_crossPen, x, clip.Top, x, clip.Bottom)
            End If
        End If
    End Sub

    Private Function LocalSelection() As Rectangle
        Dim sel = _session.SelectionVirtual
        If Not RegionHelper.IsValidCaptureRegion(sel) Then Return Rectangle.Empty
        ' Intersect with this monitor, then convert to local
        Dim inter = MonitorCaptureHelper.IntersectVirtual(sel, _monitorBounds)
        If inter.Width <= 0 OrElse inter.Height <= 0 Then Return Rectangle.Empty
        Return MonitorCaptureHelper.ToLocalRect(_monitorBounds, inter)
    End Function

    Private Function ToVirtual(local As Point) As Point
        Return MonitorCaptureHelper.ToVirtualPoint(_monitorBounds, local)
    End Function

    ''' <summary>
    ''' Invalidates only the local dirty regions for old/new selection and crosshair (fast path).
    ''' </summary>
    Public Sub InvalidateVirtualRegions(oldSelVirtual As Rectangle, newSelVirtual As Rectangle,
                                        oldCursorVirtual As Point, newCursorVirtual As Point,
                                        includeCrosshair As Boolean)
        Dim client As New Rectangle(0, 0, ClientSize.Width, ClientSize.Height)
        Const pad = 3

        InvalidateLocalOfVirtual(oldSelVirtual, client, pad)
        InvalidateLocalOfVirtual(newSelVirtual, client, pad)

        If includeCrosshair Then
            InvalidateCrosshairAt(oldCursorVirtual, client)
            InvalidateCrosshairAt(newCursorVirtual, client)
        End If
    End Sub

    Private Sub InvalidateLocalOfVirtual(selVirtual As Rectangle, client As Rectangle, pad As Integer)
        If Not RegionHelper.IsValidCaptureRegion(selVirtual) Then Return
        Dim inter = MonitorCaptureHelper.IntersectVirtual(selVirtual, _monitorBounds)
        If inter.Width <= 0 OrElse inter.Height <= 0 Then Return
        Dim local = MonitorCaptureHelper.ToLocalRect(_monitorBounds, inter)
        Dim dirty = MonitorCaptureHelper.InflateForInvalidate(local, pad, client)
        If dirty.Width > 0 AndAlso dirty.Height > 0 Then
            Invalidate(dirty)
        End If
    End Sub

    Private Sub InvalidateCrosshairAt(cursorVirtual As Point, client As Rectangle)
        Dim local = MonitorCaptureHelper.ToLocalPoint(_monitorBounds, cursorVirtual)
        ' If cursor is far outside this monitor, skip
        If local.X < -2 OrElse local.Y < -2 OrElse local.X > ClientSize.Width + 2 OrElse local.Y > ClientSize.Height + 2 Then
            Return
        End If
        For Each strip In MonitorCaptureHelper.CrosshairDirtyRegions(local, ClientSize, 3)
            Dim r = Rectangle.Intersect(strip, client)
            If r.Width > 0 AndAlso r.Height > 0 Then
                Invalidate(r)
            End If
        Next
    End Sub

    Public Sub InvalidateAllFast()
        Invalidate()
    End Sub

    Protected Overrides Sub OnMouseDown(e As MouseEventArgs)
        MyBase.OnMouseDown(e)
        If e.Button = MouseButtons.Left Then
            Capture = True
            _session.BeginDrag(ToVirtual(e.Location))
        ElseIf e.Button = MouseButtons.Right Then
            _session.Cancel()
        End If
    End Sub

    Protected Overrides Sub OnMouseMove(e As MouseEventArgs)
        MyBase.OnMouseMove(e)
        Dim v = ToVirtual(e.Location)
        If _session.IsDragging Then
            _session.UpdateDrag(v)
        Else
            _session.UpdateCursor(v)
        End If
    End Sub

    Protected Overrides Sub OnMouseUp(e As MouseEventArgs)
        MyBase.OnMouseUp(e)
        If e.Button = MouseButtons.Left AndAlso _session.IsDragging Then
            Capture = False
            _session.CompleteDrag(ToVirtual(e.Location))
        End If
    End Sub

    Protected Overrides Sub OnMouseEnter(e As EventArgs)
        MyBase.OnMouseEnter(e)
        Cursor = Cursors.Cross
    End Sub

    Protected Overrides Sub OnKeyDown(e As KeyEventArgs)
        MyBase.OnKeyDown(e)
        If e.KeyCode = Keys.Escape Then
            _session.Cancel()
        End If
    End Sub

    Protected Overrides Sub Dispose(disposing As Boolean)
        If disposing Then
            _dimBrush.Dispose()
            _borderPen.Dispose()
            _crossPen.Dispose()
            _monitorBitmap?.Dispose()
            _monitorBitmap = Nothing
        End If
        MyBase.Dispose(disposing)
    End Sub
End Class
