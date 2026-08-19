Public Class frmScreenShotter
    Private ReadOnly _workspace As New WorkspaceModel()

    ''' <summary>True while inserting/selecting a tab so handlers do not re-enter.</summary>
    Private _creatingTab As Boolean

    ''' <summary>True when a deferred CreateNewTab is already scheduled.</summary>
    Private _newTabQueued As Boolean

    Private Sub frmScreenShotter_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        ' Menu top → tabs → (inside page) capture/zoom + drawing → canvas → status
        menuStrip.Dock = DockStyle.Top
        statusStrip.Dock = DockStyle.Bottom
        tabControl.Dock = DockStyle.Fill
        toolStrip.Dock = DockStyle.Top
        KeyPreview = True
        ApplyApplicationIcon()

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
    ''' Ctrl+C copies the current tab composite to the clipboard.
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
        If keyData = (Keys.Control Or Keys.C) Then
            CopyActiveTab()
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

    Private Sub menuCopy_Click(sender As Object, e As EventArgs) Handles menuCopy.Click
        CopyActiveTab()
    End Sub

    Private Sub menuExit_Click(sender As Object, e As EventArgs) Handles menuExit.Click
        Close()
    End Sub

    Private Sub btnCapture_Click(sender As Object, e As EventArgs) Handles btnCapture.Click
        StartCapture()
    End Sub

    Private Sub btnCopy_Click(sender As Object, e As EventArgs) Handles btnCopy.Click
        CopyActiveTab()
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
        AttachMainToolbar(tabControl.SelectedTab)
        SyncWorkspaceActiveIndex()
        UpdateStatus()
    End Sub

    Private Sub SyncWorkspaceActiveIndex()
        Dim index = tabControl.SelectedIndex
        If index >= 0 AndAlso index < _workspace.Tabs.Count Then
            _workspace.ActiveTabIndex = index
        End If
    End Sub

    Private Sub tabControl_MouseDoubleClick(sender As Object, e As MouseEventArgs) Handles tabControl.MouseDoubleClick
        Dim index = tabControl.HitTestTabIndex(e.Location)
        If index < 0 Then Return
        ' Don't rename when the double-click lands on the close glyph
        If tabControl.GetCloseButtonRect(index).Contains(e.Location) Then Return
        RenameTabAt(index)
    End Sub

    Private Sub tabControl_RequestNewTab(sender As Object, e As EventArgs) Handles tabControl.RequestNewTab
        QueueCreateNewTab()
    End Sub

    Private Sub tabControl_RequestCloseTab(sender As Object, e As TabCloseEventArgs) Handles tabControl.RequestCloseTab
        CloseTabAt(e.TabIndex)
    End Sub

    ''' <summary>
    ''' Schedules CreateNewTab once after the current UI event completes.
    ''' </summary>
    Private Sub QueueCreateNewTab()
        If _creatingTab OrElse _newTabQueued Then Return
        _newTabQueued = True
        BeginInvoke(New Action(
            Sub()
                _newTabQueued = False
                CreateNewTab()
            End Sub))
    End Sub

    Private Sub CreateNewTab()
        If _creatingTab Then Return
        _creatingTab = True
        Try
            ' Number from current tab count (close Tab 2 → next new tab is Tab 2 again)
            Dim session = _workspace.AddTab()
            Dim name = session.Name

            Dim page As New TabPage(name) With {
                .UseVisualStyleBackColor = False,
                .BackColor = Color.FromArgb(245, 245, 248),
                .Padding = New Padding(0)
            }
            Dim canvas As New ScreenshotCanvas(session)
            AddHandler canvas.SelectionChanged, AddressOf OnCanvasSelectionChanged
            AddHandler canvas.TransformChanged, AddressOf OnCanvasTransformChanged

            Dim drawStrip As New DrawingToolStrip()
            WireDrawingStrip(drawStrip, canvas)

            ' Host panel keeps toolbar / draw / canvas docking stable so the canvas
            ' always fills remaining space (never covered by the toolbars).
            Dim host = CreateTabContentHost(canvas, drawStrip)
            page.Controls.Add(host)

            tabControl.TabPages.Add(page)
            SelectContentTab(page)
            tabControl.UpdateTabItemSize()
            UpdateStatus()
        Finally
            _creatingTab = False
        End Try
    End Sub

    ''' <summary>
    ''' Builds the in-tab layout with dedicated rows so the canvas never sits under the toolbars:
    ''' row0 capture/zoom · row1 drawing · row2 canvas (100%).
    ''' </summary>
    Private Function CreateTabContentHost(canvas As ScreenshotCanvas, drawStrip As DrawingToolStrip) As TableLayoutPanel
        Dim host As New TableLayoutPanel With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 1,
            .RowCount = 3,
            .Margin = Padding.Empty,
            .Padding = Padding.Empty,
            .BackColor = Color.FromArgb(245, 245, 248),
            .Tag = TabHostTag
        }
        host.ColumnStyles.Clear()
        host.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100.0F))
        host.RowStyles.Clear()
        host.RowStyles.Add(New RowStyle(SizeType.AutoSize))           ' capture / zoom
        host.RowStyles.Add(New RowStyle(SizeType.AutoSize))           ' drawing tools
        host.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))    ' canvas

        canvas.Dock = DockStyle.Fill
        canvas.Margin = Padding.Empty
        drawStrip.Dock = DockStyle.Top
        drawStrip.Margin = Padding.Empty
        drawStrip.AutoSize = True

        ' Row 0 left empty until AttachMainToolbar places the shared toolStrip
        host.Controls.Add(drawStrip, 0, 1)
        host.Controls.Add(canvas, 0, 2)
        Return host
    End Function

    Private Const TabHostTag As String = "tab-content-host"

    Private Function GetTabContentHost(page As TabPage) As TableLayoutPanel
        If page Is Nothing Then Return Nothing
        For Each ctrl As Control In page.Controls
            Dim host = TryCast(ctrl, TableLayoutPanel)
            If host IsNot Nothing AndAlso
               TypeOf host.Tag Is String AndAlso
               String.Equals(CStr(host.Tag), TabHostTag, StringComparison.Ordinal) Then
                Return host
            End If
        Next
        Return Nothing
    End Function

    ''' <summary>
    ''' Places New Screenshot / zoom in row 0 of the active host (above drawing + canvas).
    ''' </summary>
    Private Sub AttachMainToolbar(page As TabPage)
        Dim host = GetTabContentHost(page)
        If host Is Nothing Then Return
        toolStrip.Dock = DockStyle.Top
        toolStrip.GripStyle = ToolStripGripStyle.Hidden
        toolStrip.Margin = Padding.Empty
        toolStrip.AutoSize = True
        toolStrip.Visible = True
        If toolStrip.Parent IsNot host Then
            host.Controls.Add(toolStrip, 0, 0)
        Else
            host.SetRow(toolStrip, 0)
            host.SetColumn(toolStrip, 0)
        End If
        host.PerformLayout()
    End Sub

    ''' <summary>
    ''' Unparent the shared toolbar before disposing a page (do not dispose the toolbar).
    ''' </summary>
    Private Sub DetachMainToolbarIfOn(page As TabPage)
        If page Is Nothing Then Return
        Dim host = GetTabContentHost(page)
        If host IsNot Nothing AndAlso toolStrip.Parent Is host Then
            host.Controls.Remove(toolStrip)
        ElseIf toolStrip.Parent Is page Then
            page.Controls.Remove(toolStrip)
        End If
    End Sub

    ''' <summary>
    ''' Selects a content tab and keeps workspace index in sync.
    ''' </summary>
    Private Sub SelectContentTab(page As TabPage)
        If page Is Nothing Then Return
        If Not tabControl.TabPages.Contains(page) Then Return

        If Not Object.ReferenceEquals(tabControl.SelectedTab, page) Then
            tabControl.SelectedTab = page
        End If
        If Not Object.ReferenceEquals(tabControl.SelectedTab, page) Then
            Dim idx = tabControl.TabPages.IndexOf(page)
            If idx >= 0 Then tabControl.SelectedIndex = idx
        End If

        AttachMainToolbar(page)
        SyncWorkspaceActiveIndex()
        tabControl.Invalidate()
    End Sub

    Private Sub WireDrawingStrip(drawStrip As DrawingToolStrip, canvas As ScreenshotCanvas)
        AddHandler drawStrip.SettingsChanged,
            Sub(sender As Object, e As EventArgs)
                Dim strip = TryCast(sender, DrawingToolStrip)
                If strip IsNot Nothing Then
                    canvas.ApplyDrawingSettings(strip.ActiveTool, strip.Settings)
                End If
                UpdateStatus()
            End Sub
        ' Color/size only — switching tools must not restyle the selected annotation
        AddHandler drawStrip.AppearanceChanged,
            Sub(sender As Object, e As EventArgs)
                Dim strip = TryCast(sender, DrawingToolStrip)
                If strip IsNot Nothing Then
                    canvas.SelectedBox?.ApplyStyleToSelectedAnnotation(strip.Settings)
                End If
                UpdateStatus()
            End Sub
        AddHandler canvas.PointerModeRequested,
            Sub(sender As Object, e As EventArgs)
                drawStrip.SwitchToPointer()
            End Sub
        AddHandler canvas.AnnotationSelected,
            Sub(sender As Object, e As AnnotationSelectedEventArgs)
                If e?.Annotation IsNot Nothing Then
                    drawStrip.SyncFromAnnotation(e.Annotation)
                End If
            End Sub
        ' Initial sync
        canvas.ApplyDrawingSettings(drawStrip.ActiveTool, drawStrip.Settings)
    End Sub

    Private Sub RenameTabAt(index As Integer)
        If index < 0 OrElse index >= tabControl.TabPages.Count Then Return

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
        _workspace.RenameTabAt(index, normalized)
        tabControl.UpdateTabItemSize()
        UpdateStatus()
    End Sub

    Private Sub CloseTabAt(index As Integer)
        If index < 0 OrElse index >= tabControl.TabCount Then Return

        If tabControl.TabCount <= 1 Then
            ' Last tab: clear contents but keep the header title
            Dim page = tabControl.TabPages(index)
            Dim keptTitle = page.Text
            DetachMainToolbarIfOn(page)
            DisposePageControls(page)

            _workspace.RemoveTabAt(index)
            Dim session = _workspace.AddTab(keptTitle)
            Dim canvas As New ScreenshotCanvas(session)
            AddHandler canvas.SelectionChanged, AddressOf OnCanvasSelectionChanged
            AddHandler canvas.TransformChanged, AddressOf OnCanvasTransformChanged

            Dim drawStrip As New DrawingToolStrip()
            WireDrawingStrip(drawStrip, canvas)

            Dim host = CreateTabContentHost(canvas, drawStrip)
            page.Controls.Add(host)
            tabControl.SelectedTab = page
            AttachMainToolbar(page)
            SyncWorkspaceActiveIndex()
            statusLabel.Text = "Tab cleared (last tab cannot be closed)"
            tabControl.UpdateTabItemSize()
            UpdateStatus()
            Return
        End If

        Dim closingPage = tabControl.TabPages(index)
        DetachMainToolbarIfOn(closingPage)
        DisposePageControls(closingPage)
        tabControl.TabPages.RemoveAt(index)
        _workspace.RemoveTabAt(index)

        Dim nextIndex = Math.Min(index, tabControl.TabCount - 1)
        If nextIndex >= 0 Then
            tabControl.SelectedIndex = nextIndex
        End If
        AttachMainToolbar(tabControl.SelectedTab)
        SyncWorkspaceActiveIndex()
        tabControl.UpdateTabItemSize()
        UpdateStatus()
    End Sub

    Private Sub DisposePageControls(page As TabPage)
        ' Never dispose the shared capture/zoom toolbar
        DetachMainToolbarIfOn(page)
        For Each ctrl As Control In page.Controls.Cast(Of Control)().ToList()
            If Object.ReferenceEquals(ctrl, toolStrip) Then Continue For
            Dim host = TryCast(ctrl, TableLayoutPanel)
            If host IsNot Nothing Then
                For Each child As Control In host.Controls.Cast(Of Control)().ToList()
                    If Object.ReferenceEquals(child, toolStrip) Then Continue For
                    child.Dispose()
                Next
                host.Controls.Clear()
            End If
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
        Dim host = GetTabContentHost(page)
        Dim searchIn As Control = If(host, CType(page, Control))
        For Each ctrl As Control In searchIn.Controls
            Dim canvas = TryCast(ctrl, ScreenshotCanvas)
            If canvas IsNot Nothing Then Return canvas
        Next
        ' Fallback: nested one level if host layout changes
        For Each ctrl As Control In page.Controls
            Dim canvas = TryCast(ctrl, ScreenshotCanvas)
            If canvas IsNot Nothing Then Return canvas
            For Each child As Control In ctrl.Controls
                canvas = TryCast(child, ScreenshotCanvas)
                If canvas IsNot Nothing Then Return canvas
            Next
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

    ''' <summary>
    ''' Copies the current tab composite to the clipboard for paste into other apps.
    ''' </summary>
    Private Sub CopyActiveTab()
        Dim canvas = GetActiveCanvas()
        If canvas Is Nothing Then
            statusLabel.Text = "Nothing to copy"
            Return
        End If
        If canvas.ScreenshotCount = 0 Then
            MessageBox.Show(
                Me,
                "This tab has no screenshots to copy.",
                "Copy",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information)
            Return
        End If

        Try
            Using composite = canvas.RenderTabComposite()
                If composite Is Nothing Then
                    statusLabel.Text = "Copy failed"
                    Return
                End If
                Using flat = TabExportHelper.CreateClipboardBitmap(composite)
                    If flat Is Nothing Then
                        statusLabel.Text = "Copy failed"
                        Return
                    End If
                    Clipboard.SetImage(flat)
                End Using
            End Using
            statusLabel.Text = $"Copied tab ({canvas.ScreenshotCount} screenshot(s)) to clipboard — paste with Ctrl+V"
        Catch ex As Exception
            MessageBox.Show(
                Me,
                $"Failed to copy:{Environment.NewLine}{ex.Message}",
                "Copy",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error)
            statusLabel.Text = "Copy failed"
        End Try
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
        Dim tabName = If(tabControl.SelectedTab IsNot Nothing, tabControl.SelectedTab.Text, "—")
        Dim box = canvas?.SelectedBox
        If box IsNot Nothing Then
            Dim zoomTxt = ZoomHelper.FormatZoomPercent(box.Zoom)
            Dim tool = If(canvas IsNot Nothing, DrawingHelper.ToolDisplayName(canvas.ActiveTool), "Pointer")
            Dim ink = ""
            If canvas IsNot Nothing AndAlso DrawingHelper.IsInkTool(canvas.ActiveTool) Then
                Dim s = canvas.DrawingSettings
                ink = $" · {s.OpacityPercent}% · {CInt(s.Thickness)}px"
            End If
            statusLabel.Text =
                $"{tabName}: {count} · zoom {zoomTxt} · {tool}{ink} — " &
                "Ctrl+drag moves · Shift+wheel zooms · Ctrl+wheel pans · Copy / Save / Del"
            btnZoomReset.Text = zoomTxt
        Else
            Dim tool = If(canvas IsNot Nothing, DrawingHelper.ToolDisplayName(canvas.ActiveTool), "Pointer")
            statusLabel.Text =
                $"{tabName}: {count} screenshot(s) · {tool} — drawing bar under tabs: tool, color, opacity, size"
            btnZoomReset.Text = "100%"
        End If
    End Sub
End Class
