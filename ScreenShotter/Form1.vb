Public Class frmScreenShotter
    Private ReadOnly _workspace As New WorkspaceModel()
    Private _tabCounter As Integer = 0

    Private Sub frmScreenShotter_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        ' Responsive chrome: toolstrip top, status bottom, tabs fill remainder
        toolStrip.Dock = DockStyle.Top
        statusStrip.Dock = DockStyle.Bottom
        tabControl.Dock = DockStyle.Fill

        CreateNewTab()
        UpdateStatus()
    End Sub

    Private Sub btnCapture_Click(sender As Object, e As EventArgs) Handles btnCapture.Click
        StartCapture()
    End Sub

    Private Sub btnNewTab_Click(sender As Object, e As EventArgs) Handles btnNewTab.Click
        CreateNewTab()
    End Sub

    Private Sub btnCloseTab_Click(sender As Object, e As EventArgs) Handles btnCloseTab.Click
        CloseActiveTab()
    End Sub

    Private Sub btnZoomIn_Click(sender As Object, e As EventArgs) Handles btnZoomIn.Click
        GetActiveCanvas()?.ZoomSelectedIn()
        UpdateStatus()
    End Sub

    Private Sub btnZoomOut_Click(sender As Object, e As EventArgs) Handles btnZoomOut.Click
        GetActiveCanvas()?.ZoomSelectedOut()
        UpdateStatus()
    End Sub

    Private Sub btnZoomReset_Click(sender As Object, e As EventArgs) Handles btnZoomReset.Click
        GetActiveCanvas()?.ZoomSelectedReset()
        UpdateStatus()
    End Sub

    Private Sub tabControl_SelectedIndexChanged(sender As Object, e As EventArgs) Handles tabControl.SelectedIndexChanged
        If tabControl.SelectedIndex >= 0 AndAlso tabControl.SelectedIndex < _workspace.Tabs.Count Then
            _workspace.ActiveTabIndex = tabControl.SelectedIndex
        End If
        UpdateStatus()
    End Sub

    Private Sub CreateNewTab()
        _tabCounter += 1
        Dim name = $"Tab {_tabCounter}"
        Dim session = _workspace.AddTab(name)

        Dim page As New TabPage(name) With {
            .UseVisualStyleBackColor = True,
            .Padding = New Padding(0)
        }
        Dim canvas As New ScreenshotCanvas(session)
        AddHandler canvas.SelectionChanged, AddressOf OnCanvasSelectionChanged
        AddHandler canvas.TransformChanged, AddressOf OnCanvasTransformChanged
        page.Controls.Add(canvas)
        tabControl.TabPages.Add(page)
        tabControl.SelectedTab = page
        UpdateStatus()
    End Sub

    Private Sub CloseActiveTab()
        Dim closeIndex = tabControl.SelectedIndex
        If closeIndex < 0 Then Return

        If tabControl.TabPages.Count <= 1 Then
            Dim page = tabControl.TabPages(0)
            DisposePageControls(page)

            _workspace.RemoveTabAt(0)
            Dim session = _workspace.AddTab(page.Text)
            Dim canvas As New ScreenshotCanvas(session)
            AddHandler canvas.SelectionChanged, AddressOf OnCanvasSelectionChanged
            AddHandler canvas.TransformChanged, AddressOf OnCanvasTransformChanged
            page.Controls.Add(canvas)
            statusLabel.Text = "Tab cleared (last tab cannot be closed)"
            UpdateStatus()
            Return
        End If

        Dim closingPage = tabControl.TabPages(closeIndex)
        DisposePageControls(closingPage)
        tabControl.TabPages.RemoveAt(closeIndex)
        _workspace.RemoveTabAt(closeIndex)
        UpdateStatus()
    End Sub

    Private Shared Sub DisposePageControls(page As TabPage)
        For Each ctrl As Control In page.Controls.Cast(Of Control)().ToList()
            ctrl.Dispose()
        Next
        page.Controls.Clear()
    End Sub

    Private Sub StartCapture()
        Dim previousState = WindowState
        Try
            Dim monitorCount = Screen.AllScreens.Length
            statusLabel.Text =
                $"Capturing on {monitorCount} monitor(s)… drag a rectangle (can span displays); Esc or right-click to cancel"
            Application.DoEvents()

            WindowState = FormWindowState.Minimized
            Application.DoEvents()
            Threading.Thread.Sleep(200)

            Dim session = CaptureSession.Run()
            If session.Accepted Then
                Dim captured = session.TakeResult()
                If captured IsNot Nothing Then
                    Dim canvas = GetActiveCanvas()
                    If canvas IsNot Nothing Then
                        Dim w = captured.Width
                        Dim h = captured.Height
                        canvas.AddScreenshotImage(captured)
                        statusLabel.Text =
                            $"Captured {w}×{h} at full size (100%) — drag edges to resize · Ctrl+wheel or ± to zoom"
                    Else
                        captured.Dispose()
                        statusLabel.Text = "Capture succeeded but no active canvas"
                    End If
                Else
                    statusLabel.Text = "Capture produced no image"
                End If
            Else
                statusLabel.Text = "Capture cancelled"
            End If
        Finally
            If previousState = FormWindowState.Minimized Then
                WindowState = FormWindowState.Normal
            Else
                WindowState = previousState
            End If
            Activate()
            BringToFront()
            UpdateStatus()
        End Try
    End Sub

    Private Function GetActiveCanvas() As ScreenshotCanvas
        Dim page = tabControl.SelectedTab
        If page Is Nothing Then Return Nothing
        For Each ctrl As Control In page.Controls
            Dim canvas = TryCast(ctrl, ScreenshotCanvas)
            If canvas IsNot Nothing Then Return canvas
        Next
        Return Nothing
    End Function

    Private Sub OnCanvasSelectionChanged(sender As Object, e As EventArgs)
        UpdateStatus()
    End Sub

    Private Sub OnCanvasTransformChanged(sender As Object, e As TransformChangedEventArgs)
        UpdateStatus()
    End Sub

    Private Sub UpdateStatus()
        Dim canvas = GetActiveCanvas()
        Dim count = If(canvas IsNot Nothing, canvas.Session.Items.Count, 0)
        Dim tabName = If(tabControl.SelectedTab IsNot Nothing, tabControl.SelectedTab.Text, "—")
        Dim box = canvas?.SelectedBox
        If box IsNot Nothing Then
            Dim nat = box.NaturalSize
            Dim zoomTxt = ZoomHelper.FormatZoomPercent(box.Zoom)
            statusLabel.Text =
                $"{tabName}: {count} · {nat.Width}×{nat.Height} native · frame {box.Width}×{box.Height} · zoom {zoomTxt} — " &
                "move body · edge stretch · corner aspect · Ctrl+wheel/± zoom · wheel scrolls · Shift/middle pan · double-click 100%"
            btnZoomReset.Text = zoomTxt
        Else
            statusLabel.Text =
                $"{tabName}: {count} screenshot(s) — New Screenshot · select one to zoom/resize"
            btnZoomReset.Text = "100%"
        End If
    End Sub
End Class
