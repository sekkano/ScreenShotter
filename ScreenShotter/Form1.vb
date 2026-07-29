Public Class frmScreenShotter
    Private ReadOnly _workspace As New WorkspaceModel()

    ''' <summary>Tag value for the trailing "+" tab used to create new tabs.</summary>
    Private Const NewTabTag As String = "new-tab-placeholder"

    ''' <summary>Hit padding around the close glyph in tab headers.</summary>
    Private Const CloseHitPad As Integer = 4

    Private Sub frmScreenShotter_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        ' Responsive chrome: menu + toolstrip top, status bottom, tabs fill remainder
        menuStrip.Dock = DockStyle.Top
        toolStrip.Dock = DockStyle.Top
        statusStrip.Dock = DockStyle.Bottom
        tabControl.Dock = DockStyle.Fill
        KeyPreview = True
        ApplyApplicationIcon()

        EnsurePlusTab()
        CreateNewTab()
        UpdateStatus()
    End Sub

    ''' <summary>
    ''' Uses the embedded application icon for the window title bar / taskbar.
    ''' </summary>
    Private Sub ApplyApplicationIcon()
        Try
            Dim exePath = Application.ExecutablePath
            If Not String.IsNullOrEmpty(exePath) AndAlso IO.File.Exists(exePath) Then
                Dim extracted = Icon.ExtractAssociatedIcon(exePath)
                If extracted IsNot Nothing Then
                    Me.Icon = extracted
                    Return
                End If
            End If
            ' Design-time / fallback: load app.ico next to the project output
            Dim icoBeside = IO.Path.Combine(Application.StartupPath, "app.ico")
            If IO.File.Exists(icoBeside) Then
                Me.Icon = New Icon(icoBeside)
            End If
        Catch
            ' Non-fatal if icon cannot be loaded
        End Try
    End Sub

    ''' <summary>
    ''' Delete / Backspace removes the selected screenshot on the active tab.
    ''' Ctrl+Z / Ctrl+Y undo and redo on the active tab.
    ''' Ctrl+S saves the current tab composite.
    ''' </summary>
    Protected Overrides Function ProcessCmdKey(ByRef msg As Message, keyData As Keys) As Boolean
        If keyData = Keys.Delete OrElse keyData = Keys.Back Then
            Dim canvas = GetActiveCanvas()
            If canvas IsNot Nothing AndAlso canvas.SelectedBox IsNot Nothing Then
                If canvas.RemoveSelectedScreenshot() Then
                    UpdateStatus()
                    Return True
                End If
            End If
        End If
        If keyData = (Keys.Control Or Keys.Z) Then
            Dim canvas = GetActiveCanvas()
            If canvas IsNot Nothing AndAlso canvas.Undo() Then
                UpdateStatus()
                Return True
            End If
        End If
        If keyData = (Keys.Control Or Keys.Y) OrElse keyData = (Keys.Control Or Keys.Shift Or Keys.Z) Then
            Dim canvas = GetActiveCanvas()
            If canvas IsNot Nothing AndAlso canvas.Redo() Then
                UpdateStatus()
                Return True
            End If
        End If
        If keyData = (Keys.Control Or Keys.S) Then
            SaveActiveTab()
            Return True
        End If
        Return MyBase.ProcessCmdKey(msg, keyData)
    End Function

    ''' <summary>
    ''' When the window is inactive, the first click both activates and reaches the control
    ''' (buttons, screenshots) — no separate "focus click" required.
    ''' </summary>
    Protected Overrides Sub WndProc(ByRef m As Message)
        If m.Msg = MouseActivateHelper.WM_MOUSEACTIVATE Then
            MyBase.WndProc(m)
            ' Prefer activate + pass click through; never eat the activating click.
            m.Result = MouseActivateHelper.ActivateAndPassClick
            Return
        End If
        MyBase.WndProc(m)
    End Sub

    Private Sub menuSave_Click(sender As Object, e As EventArgs) Handles menuSave.Click
        SaveActiveTab()
    End Sub

    Private Sub menuExit_Click(sender As Object, e As EventArgs) Handles menuExit.Click
        Close()
    End Sub

    Private Sub btnCapture_Click(sender As Object, e As EventArgs) Handles btnCapture.Click
        StartCapture()
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
        SyncWorkspaceActiveIndex()
        UpdateStatus()
    End Sub

    ''' <summary>
    ''' Keep workspace index aligned with real content tabs (skip the trailing +).
    ''' </summary>
    Private Sub SyncWorkspaceActiveIndex()
        Dim contentIndex = ContentTabIndex(tabControl.SelectedIndex)
        If contentIndex >= 0 AndAlso contentIndex < _workspace.Tabs.Count Then
            _workspace.ActiveTabIndex = contentIndex
        End If
    End Sub

    Private Sub tabControl_MouseDoubleClick(sender As Object, e As MouseEventArgs) Handles tabControl.MouseDoubleClick
        Dim index = HitTestTabIndex(e.Location)
        If index < 0 OrElse IsPlusTab(index) Then Return
        ' Don't rename when the double-click lands on the close glyph
        If GetCloseButtonRect(index).Contains(e.Location) Then Return
        RenameTabAt(index)
    End Sub

    Private Sub tabControl_Selecting(sender As Object, e As TabControlCancelEventArgs) Handles tabControl.Selecting
        If e.TabPage Is Nothing Then Return
        If IsPlusPage(e.TabPage) Then
            e.Cancel = True
            CreateNewTab()
        End If
    End Sub

    Private Sub tabControl_MouseDown(sender As Object, e As MouseEventArgs) Handles tabControl.MouseDown
        If e.Button <> MouseButtons.Left Then Return
        Dim index = HitTestTabIndex(e.Location)
        If index < 0 OrElse IsPlusTab(index) Then Return

        ' Close glyph only — "+" is handled in Selecting (avoids double-create)
        If GetCloseButtonRect(index).Contains(e.Location) Then
            CloseTabAt(index)
        End If
    End Sub

    Private Sub tabControl_DrawItem(sender As Object, e As DrawItemEventArgs) Handles tabControl.DrawItem
        If e.Index < 0 OrElse e.Index >= tabControl.TabCount Then Return

        Dim page = tabControl.TabPages(e.Index)
        Dim bounds = e.Bounds
        Dim selected = (e.State And DrawItemState.Selected) = DrawItemState.Selected
        Dim isPlus = IsPlusPage(page)

        Using backBrush As New SolidBrush(If(selected, SystemColors.Window, SystemColors.Control))
            e.Graphics.FillRectangle(backBrush, bounds)
        End Using

        ' Subtle border under the header
        Using pen As New Pen(SystemColors.ControlDark)
            e.Graphics.DrawLine(pen, bounds.Left, bounds.Bottom - 1, bounds.Right, bounds.Bottom - 1)
        End Using

        Dim textColor = If(selected, SystemColors.ControlText, SystemColors.GrayText)
        Dim flags = TextFormatFlags.HorizontalCenter Or
                    TextFormatFlags.VerticalCenter Or
                    TextFormatFlags.EndEllipsis Or
                    TextFormatFlags.NoPadding

        If isPlus Then
            TextRenderer.DrawText(
                e.Graphics,
                "+",
                New Font(Font, FontStyle.Bold),
                bounds,
                textColor,
                flags)
            Return
        End If

        Dim closeRect = GetCloseButtonRect(index:=e.Index, tabBounds:=bounds)
        Dim textRect = New Rectangle(
            bounds.Left + 6,
            bounds.Top,
            Math.Max(8, closeRect.Left - bounds.Left - 8),
            bounds.Height)

        TextRenderer.DrawText(
            e.Graphics,
            page.Text,
            Font,
            textRect,
            textColor,
            TextFormatFlags.Left Or TextFormatFlags.VerticalCenter Or TextFormatFlags.EndEllipsis Or TextFormatFlags.NoPadding)

        ' Close glyph (×)
        Dim closeColor = If(selected, SystemColors.ControlText, SystemColors.GrayText)
        TextRenderer.DrawText(
            e.Graphics,
            "×",
            Font,
            closeRect,
            closeColor,
            TextFormatFlags.HorizontalCenter Or TextFormatFlags.VerticalCenter Or TextFormatFlags.NoPadding)
    End Sub

    Private Sub CreateNewTab()
        EnsurePlusTab()

        ' Number from current content tab count (close Tab 2 → next new tab is Tab 2 again)
        Dim session = _workspace.AddTab()
        Dim name = session.Name

        Dim page As New TabPage(name) With {
            .UseVisualStyleBackColor = True,
            .Padding = New Padding(0)
        }
        Dim canvas As New ScreenshotCanvas(session)
        AddHandler canvas.SelectionChanged, AddressOf OnCanvasSelectionChanged
        AddHandler canvas.TransformChanged, AddressOf OnCanvasTransformChanged

        Dim drawStrip As New DrawingToolStrip()
        WireDrawingStrip(drawStrip, canvas)

        ' Dock: Fill first, then Top so the drawing bar sits under tab headers
        page.Controls.Add(canvas)
        page.Controls.Add(drawStrip)

        Dim insertAt = PlusTabIndex()
        If insertAt < 0 Then
            tabControl.TabPages.Add(page)
            EnsurePlusTab()
        Else
            tabControl.TabPages.Insert(insertAt, page)
        End If

        tabControl.SelectedTab = page
        SyncWorkspaceActiveIndex()
        UpdateTabItemSize()
        UpdateStatus()
    End Sub

    Private Sub EnsurePlusTab()
        Dim plusIndex = PlusTabIndex()
        If plusIndex >= 0 Then
            ' Keep the placeholder last
            If plusIndex <> tabControl.TabCount - 1 Then
                Dim plusPage = tabControl.TabPages(plusIndex)
                tabControl.TabPages.RemoveAt(plusIndex)
                tabControl.TabPages.Add(plusPage)
            End If
            Return
        End If

        Dim plus As New TabPage("+") With {
            .Tag = NewTabTag,
            .ToolTipText = "New tab",
            .UseVisualStyleBackColor = True
        }
        tabControl.TabPages.Add(plus)
        UpdateTabItemSize()
    End Sub

    Private Sub UpdateTabItemSize()
        ' Fixed owner-draw width: enough room for name + close glyph (or just "+")
        Dim w = 110
        Using g = tabControl.CreateGraphics()
            For Each page As TabPage In tabControl.TabPages
                If IsPlusPage(page) Then
                    w = Math.Max(w, 36)
                    Continue For
                End If
                Dim textW = TextRenderer.MeasureText(g, page.Text, Font).Width
                w = Math.Max(w, Math.Min(220, textW + 36))
            Next
        End Using
        Dim nextSize As New Size(w, 24)
        If tabControl.ItemSize <> nextSize Then
            tabControl.ItemSize = nextSize
        End If
    End Sub

    Private Function IsPlusTab(index As Integer) As Boolean
        If index < 0 OrElse index >= tabControl.TabCount Then Return False
        Return IsPlusPage(tabControl.TabPages(index))
    End Function

    Private Shared Function IsPlusPage(page As TabPage) As Boolean
        If page Is Nothing Then Return False
        Return TypeOf page.Tag Is String AndAlso String.Equals(CStr(page.Tag), NewTabTag, StringComparison.Ordinal)
    End Function

    Private Function PlusTabIndex() As Integer
        For i = 0 To tabControl.TabCount - 1
            If IsPlusTab(i) Then Return i
        Next
        Return -1
    End Function

    ''' <summary>
    ''' Maps a TabControl index to the workspace content index (skips the + tab).
    ''' </summary>
    Private Function ContentTabIndex(tabIndex As Integer) As Integer
        If tabIndex < 0 OrElse IsPlusTab(tabIndex) Then Return -1
        Dim plus = PlusTabIndex()
        If plus >= 0 AndAlso tabIndex > plus Then
            Return tabIndex - 1
        End If
        Return tabIndex
    End Function

    Private Sub WireDrawingStrip(drawStrip As DrawingToolStrip, canvas As ScreenshotCanvas)
        AddHandler drawStrip.SettingsChanged,
            Sub(sender As Object, e As EventArgs)
                Dim strip = TryCast(sender, DrawingToolStrip)
                If strip IsNot Nothing Then
                    canvas.ApplyDrawingSettings(strip.ActiveTool, strip.Settings)
                End If
                UpdateStatus()
            End Sub
        ' Initial sync
        canvas.ApplyDrawingSettings(drawStrip.ActiveTool, drawStrip.Settings)
    End Sub

    ''' <summary>
    ''' Which tab header contains the client point, or -1.
    ''' </summary>
    Private Function HitTestTabIndex(clientPoint As Point) As Integer
        For i = 0 To tabControl.TabCount - 1
            If tabControl.GetTabRect(i).Contains(clientPoint) Then
                Return i
            End If
        Next
        Return -1
    End Function

    Private Function GetCloseButtonRect(index As Integer, Optional tabBounds As Rectangle? = Nothing) As Rectangle
        Dim bounds = If(tabBounds, tabControl.GetTabRect(index))
        Dim size = 14
        Dim x = bounds.Right - size - 6
        Dim y = bounds.Top + (bounds.Height - size) \ 2
        Return New Rectangle(x - CloseHitPad, y - CloseHitPad, size + CloseHitPad * 2, size + CloseHitPad * 2)
    End Function

    Private Sub RenameTabAt(index As Integer)
        If index < 0 OrElse index >= tabControl.TabPages.Count OrElse IsPlusTab(index) Then Return

        Dim page = tabControl.TabPages(index)
        Dim proposed = Interaction.InputBox(
            "Enter a new name for this tab:",
            "Rename Tab",
            page.Text)

        Dim normalized = TabNamingHelper.NormalizeTabName(proposed)
        If normalized Is Nothing Then
            ' Cancelled or blank — leave name unchanged
            Return
        End If

        page.Text = normalized
        Dim contentIndex = ContentTabIndex(index)
        If contentIndex >= 0 Then
            _workspace.RenameTabAt(contentIndex, normalized)
        End If
        UpdateTabItemSize()
        UpdateStatus()
    End Sub

    Private Sub CloseTabAt(index As Integer)
        If index < 0 OrElse IsPlusTab(index) Then Return

        Dim contentIndex = ContentTabIndex(index)
        If contentIndex < 0 Then Return

        Dim contentCount = ContentTabCount()
        If contentCount <= 1 Then
            ' Last real tab: clear contents but keep the header title
            Dim page = tabControl.TabPages(index)
            Dim keptTitle = page.Text
            DisposePageControls(page)

            _workspace.RemoveTabAt(contentIndex)
            Dim session = _workspace.AddTab(keptTitle)
            Dim canvas As New ScreenshotCanvas(session)
            AddHandler canvas.SelectionChanged, AddressOf OnCanvasSelectionChanged
            AddHandler canvas.TransformChanged, AddressOf OnCanvasTransformChanged

            Dim drawStrip As New DrawingToolStrip()
            WireDrawingStrip(drawStrip, canvas)

            page.Controls.Add(canvas)
            page.Controls.Add(drawStrip)
            tabControl.SelectedTab = page
            SyncWorkspaceActiveIndex()
            statusLabel.Text = "Tab cleared (last tab cannot be closed)"
            UpdateStatus()
            Return
        End If

        Dim closingPage = tabControl.TabPages(index)
        DisposePageControls(closingPage)
        tabControl.TabPages.RemoveAt(index)
        _workspace.RemoveTabAt(contentIndex)

        ' Prefer a neighboring content tab after close
        Dim nextContent = Math.Min(contentIndex, ContentTabCount() - 1)
        Dim selectIndex = ContentIndexToTabIndex(nextContent)
        If selectIndex >= 0 Then
            tabControl.SelectedIndex = selectIndex
        End If
        EnsurePlusTab()
        SyncWorkspaceActiveIndex()
        UpdateTabItemSize()
        UpdateStatus()
    End Sub

    Private Function ContentTabCount() As Integer
        Dim n = 0
        For i = 0 To tabControl.TabCount - 1
            If Not IsPlusTab(i) Then n += 1
        Next
        Return n
    End Function

    Private Function ContentIndexToTabIndex(contentIndex As Integer) As Integer
        Dim seen = 0
        For i = 0 To tabControl.TabCount - 1
            If IsPlusTab(i) Then Continue For
            If seen = contentIndex Then Return i
            seen += 1
        Next
        Return -1
    End Function

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
        If page Is Nothing OrElse IsPlusPage(page) Then Return Nothing
        For Each ctrl As Control In page.Controls
            Dim canvas = TryCast(ctrl, ScreenshotCanvas)
            If canvas IsNot Nothing Then Return canvas
        Next
        Return Nothing
    End Function

    ''' <summary>
    ''' Saves everything on the current tab (positions, sizes, zoom, overlap) as one image.
    ''' </summary>
    Private Sub SaveActiveTab()
        Dim canvas = GetActiveCanvas()
        If canvas Is Nothing Then
            statusLabel.Text = "Nothing to save"
            Return
        End If
        If canvas.ScreenshotCount = 0 Then
            MessageBox.Show(
                Me,
                "This tab has no screenshots to save.",
                "Save",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information)
            Return
        End If

        Dim tabName = If(tabControl.SelectedTab IsNot Nothing, tabControl.SelectedTab.Text, "Tab")
        Dim safeName = String.Join("_", tabName.Split(IO.Path.GetInvalidFileNameChars()))

        Using dlg As New SaveFileDialog()
            dlg.Title = "Save current tab"
            dlg.Filter = "PNG image (*.png)|*.png|JPEG image (*.jpg;*.jpeg)|*.jpg;*.jpeg|Bitmap (*.bmp)|*.bmp"
            dlg.FilterIndex = 1
            dlg.DefaultExt = "png"
            dlg.AddExtension = True
            dlg.FileName = $"{safeName}_{DateTime.Now:yyyyMMdd_HHmmss}.png"
            dlg.OverwritePrompt = True

            If dlg.ShowDialog(Me) <> DialogResult.OK Then
                statusLabel.Text = "Save cancelled"
                Return
            End If

            Try
                If canvas.SaveTabComposite(dlg.FileName) Then
                    statusLabel.Text = $"Saved tab ({canvas.ScreenshotCount} screenshot(s)) → {dlg.FileName}"
                Else
                    MessageBox.Show(
                        Me,
                        "Could not create the tab image.",
                        "Save",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning)
                    statusLabel.Text = "Save failed"
                End If
            Catch ex As Exception
                MessageBox.Show(
                    Me,
                    $"Failed to save:{Environment.NewLine}{ex.Message}",
                    "Save",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error)
                statusLabel.Text = "Save failed"
            End Try
        End Using
    End Sub

    Private Sub OnCanvasSelectionChanged(sender As Object, e As EventArgs)
        UpdateStatus()
    End Sub

    Private Sub OnCanvasTransformChanged(sender As Object, e As TransformChangedEventArgs)
        UpdateStatus()
    End Sub

    Private Sub UpdateStatus()
        Dim canvas = GetActiveCanvas()
        Dim count = If(canvas IsNot Nothing, canvas.Session.Items.Count, 0)
        Dim tabName = If(tabControl.SelectedTab IsNot Nothing AndAlso Not IsPlusPage(tabControl.SelectedTab),
            tabControl.SelectedTab.Text, "—")
        Dim box = canvas?.SelectedBox
        If box IsNot Nothing Then
            Dim nat = box.NaturalSize
            Dim zoomTxt = ZoomHelper.FormatZoomPercent(box.Zoom)
            Dim tool = If(canvas IsNot Nothing, DrawingHelper.ToolDisplayName(canvas.ActiveTool), "Pointer")
            Dim ink = ""
            If canvas IsNot Nothing AndAlso DrawingHelper.IsInkTool(canvas.ActiveTool) Then
                Dim s = canvas.DrawingSettings
                ink = $" · {s.OpacityPercent}% · {CInt(s.Thickness)}px"
            End If
            statusLabel.Text =
                $"{tabName}: {count} · zoom {zoomTxt} · {tool}{ink} — " &
                "Ctrl+drag moves · Shift+wheel zooms · Ctrl+wheel pans · File → Save / Del"
            btnZoomReset.Text = zoomTxt
        Else
            Dim tool = If(canvas IsNot Nothing, DrawingHelper.ToolDisplayName(canvas.ActiveTool), "Pointer")
            statusLabel.Text =
                $"{tabName}: {count} screenshot(s) · {tool} — drawing bar under tabs: tool, color, opacity, size"
            btnZoomReset.Text = "100%"
        End If
    End Sub
End Class
