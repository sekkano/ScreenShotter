''' <summary>
''' Annotation toolbar under the tab headers: Pointer, drawing-tool dropdown,
''' color, transparency, and thickness / text size.
''' </summary>
Public Class DrawingToolStrip
    Inherits ClickThroughToolStrip

    Private ReadOnly _btnPointer As ToolStripButton
    Private ReadOnly _btnDraw As ToolStripButton
    Private ReadOnly _cmbTool As ToolStripComboBox
    Private ReadOnly _lblColor As ToolStripLabel
    Private ReadOnly _btnColor As ToolStripButton
    Private ReadOnly _cmbOpacity As ToolStripComboBox
    Private ReadOnly _cmbThickness As ToolStripComboBox
    Private ReadOnly _lblOpacity As ToolStripLabel
    Private ReadOnly _lblSize As ToolStripLabel
    Private ReadOnly _settings As New DrawingSettings()
    Private _modeIsPointer As Boolean = True
    Private _suppressEvents As Boolean

    Public Event SettingsChanged As EventHandler
    ''' <summary>Fired when color / opacity / size changes (not when only switching tools).</summary>
    Public Event AppearanceChanged As EventHandler

    Private Shared ReadOnly OpacityChoices As Integer() = {20, 30, 40, 50, 60, 70, 80, 90, 100}
    Private Shared ReadOnly ThicknessChoices As Single() = {2, 4, 8, 12, 16, 20, 28, 36, 48, 64}
    Private Shared ReadOnly FontSizeChoices As Single() = {12, 16, 20, 24, 28, 36, 48, 64, 72, 96}

    Private Shared ReadOnly ToolOrder As DrawingTool() = {
        DrawingTool.Highlighter,
        DrawingTool.Pen,
        DrawingTool.Blur,
        DrawingTool.Rectangle,
        DrawingTool.Arrow,
        DrawingTool.Text
    }

    Public Sub New()
        MyBase.New()
        GripStyle = ToolStripGripStyle.Hidden
        Dock = DockStyle.Top
        Padding = New Padding(4, 2, 4, 2)
        BackColor = Color.FromArgb(250, 250, 252)

        _btnPointer = New ToolStripButton("Pointer") With {
            .CheckOnClick = False,
            .Checked = True,
            .DisplayStyle = ToolStripItemDisplayStyle.Text,
            .ToolTipText = "Select screenshots and annotations (Ctrl+drag always moves a screenshot)"
        }
        _btnDraw = New ToolStripButton("Draw") With {
            .CheckOnClick = False,
            .Checked = False,
            .DisplayStyle = ToolStripItemDisplayStyle.Text,
            .ToolTipText = "Draw with the selected tool"
        }

        _cmbTool = New ToolStripComboBox("cmbTool") With {
            .DropDownStyle = ComboBoxStyle.DropDownList,
            .AutoSize = False,
            .Width = 110,
            .ToolTipText = "Drawing tool"
        }
        For Each t In ToolOrder
            _cmbTool.Items.Add(DrawingHelper.ToolDisplayName(t))
        Next
        _cmbTool.SelectedIndex = 0

        _lblColor = New ToolStripLabel("Color:") With {.ForeColor = Color.DimGray}
        _btnColor = New ToolStripButton("  ") With {
            .DisplayStyle = ToolStripItemDisplayStyle.Text,
            .ToolTipText = "Color",
            .AutoSize = False,
            .Width = 36
        }
        UpdateColorSwatch()

        _lblOpacity = New ToolStripLabel("Opacity:") With {.ForeColor = Color.DimGray}
        _cmbOpacity = New ToolStripComboBox("cmbOpacity") With {
            .DropDownStyle = ComboBoxStyle.DropDownList,
            .AutoSize = False,
            .Width = 72,
            .ToolTipText = "Transparency / opacity"
        }
        For Each pct In OpacityChoices
            _cmbOpacity.Items.Add($"{pct}%")
        Next
        SelectOpacity(DrawingHelper.DefaultOpacityPercent)

        _lblSize = New ToolStripLabel("Size:") With {.ForeColor = Color.DimGray}
        _cmbThickness = New ToolStripComboBox("cmbThickness") With {
            .DropDownStyle = ComboBoxStyle.DropDownList,
            .AutoSize = False,
            .Width = 72,
            .ToolTipText = "Stroke thickness or text size"
        }
        RebuildSizeChoices(forText:=False)
        SelectThickness(DrawingHelper.DefaultThickness)

        Items.Add(_btnPointer)
        Items.Add(_btnDraw)
        Items.Add(New ToolStripSeparator())
        Items.Add(_cmbTool)
        Items.Add(_lblColor)
        Items.Add(_btnColor)
        Items.Add(_lblOpacity)
        Items.Add(_cmbOpacity)
        Items.Add(_lblSize)
        Items.Add(_cmbThickness)

        AddHandler _btnPointer.Click, AddressOf OnPointerClick
        AddHandler _btnDraw.Click, AddressOf OnDrawClick
        AddHandler _cmbTool.SelectedIndexChanged, AddressOf OnToolChanged
        AddHandler _btnColor.Click, AddressOf OnColorClick
        AddHandler _cmbOpacity.SelectedIndexChanged, AddressOf OnOpacityChanged
        AddHandler _cmbThickness.SelectedIndexChanged, AddressOf OnThicknessChanged
        AddHandler _cmbTool.DropDown, AddressOf OnDrawUiActivated
        AddHandler _cmbTool.Click, AddressOf OnDrawUiActivated
        AddHandler _cmbOpacity.DropDown, AddressOf OnDrawUiActivated
        AddHandler _cmbThickness.DropDown, AddressOf OnDrawUiActivated

        UpdateControlsForTool(_settings.Tool)
    End Sub

    Private Sub OnDrawClick(sender As Object, e As EventArgs)
        EnterDrawMode()
    End Sub

    Private Sub OnDrawUiActivated(sender As Object, e As EventArgs)
        If _suppressEvents Then Return
        ' Stay in Pointer when restyling a selected annotation (don't kick into Draw)
        If _modeIsPointer Then Return
        EnterDrawMode()
    End Sub

    Private Sub EnterDrawMode()
        Dim tool = ToolFromComboIndex(_cmbTool.SelectedIndex)
        _settings.SelectTool(tool)
        SyncAppearanceControlsFromSettings()
        _modeIsPointer = False
        SyncModeButtons()
        RaiseSettingsChanged()
    End Sub

    Private Sub SyncModeButtons()
        _btnPointer.Checked = _modeIsPointer
        _btnDraw.Checked = Not _modeIsPointer
    End Sub

    <System.ComponentModel.Browsable(False)>
    <System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)>
    Public ReadOnly Property ActiveTool As DrawingTool
        Get
            If _modeIsPointer Then Return DrawingTool.Pointer
            Return _settings.Tool
        End Get
    End Property

    <System.ComponentModel.Browsable(False)>
    <System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)>
    Public ReadOnly Property Settings As DrawingSettings
        Get
            Return _settings
        End Get
    End Property

    Private Sub RaiseSettingsChanged()
        RaiseEvent SettingsChanged(Me, EventArgs.Empty)
    End Sub

    Private Sub RaiseAppearanceChanged()
        RaiseEvent AppearanceChanged(Me, EventArgs.Empty)
    End Sub

    Private Sub OnPointerClick(sender As Object, e As EventArgs)
        SwitchToPointer()
    End Sub

    ''' <summary>Leaves draw mode and selects Pointer (e.g. right-click on a screenshot).</summary>
    Public Sub SwitchToPointer()
        _modeIsPointer = True
        SyncModeButtons()
        RaiseSettingsChanged()
    End Sub

    ''' <summary>True when Pointer is the active mode.</summary>
    Public ReadOnly Property IsPointerMode As Boolean
        Get
            Return _modeIsPointer
        End Get
    End Property

    ''' <summary>
    ''' Loads the tool dropdown + color/size from a selected annotation.
    ''' Does not change Pointer vs Draw — only an explicit Pointer click or right-click does that.
    ''' Does not raise AppearanceChanged (avoids rewriting the selection).
    ''' </summary>
    Public Sub SyncFromAnnotation(ann As AnnotationBase)
        If ann Is Nothing Then Return

        Dim tool As DrawingTool
        If TypeOf ann Is RectAnnotation Then
            tool = DrawingTool.Rectangle
        ElseIf TypeOf ann Is ArrowAnnotation Then
            tool = DrawingTool.Arrow
        ElseIf TypeOf ann Is TextAnnotation Then
            tool = DrawingTool.Text
        Else
            Return
        End If

        _suppressEvents = True
        Try
            _settings.SelectTool(tool)
            _settings.BaseColor = Color.FromArgb(255, ann.Color)
            If tool = DrawingTool.Text Then
                _settings.Thickness = AnnotationHelper.ClampFontSize(ann.NativeSize)
            Else
                _settings.Thickness = DrawingHelper.ClampThickness(ann.NativeSize)
            End If

            Dim idx = Array.IndexOf(ToolOrder, tool)
            If idx >= 0 Then _cmbTool.SelectedIndex = idx
            UpdateControlsForTool(tool)
            SyncAppearanceControlsFromSettings()
        Finally
            _suppressEvents = False
        End Try

        RaiseSettingsChanged()
    End Sub

    ''' <summary>
    ''' Loads tool / color / size from a selected freehand stroke without changing Pointer vs Draw.
    ''' </summary>
    Public Sub SyncFromStroke(stroke As InkStroke)
        If stroke Is Nothing OrElse Not DrawingHelper.IsInkTool(stroke.Tool) Then Return

        _suppressEvents = True
        Try
            _settings.SelectTool(stroke.Tool)
            _settings.BaseColor = Color.FromArgb(255, stroke.Color)
            _settings.OpacityPercent = DrawingHelper.ClampOpacityPercent(
                CInt(Math.Round(stroke.Color.A / 255.0 * 100.0)))
            _settings.Thickness = DrawingHelper.ClampThickness(stroke.NativeWidth)

            Dim idx = Array.IndexOf(ToolOrder, stroke.Tool)
            If idx >= 0 Then _cmbTool.SelectedIndex = idx
            UpdateControlsForTool(stroke.Tool)
            SyncAppearanceControlsFromSettings()
        Finally
            _suppressEvents = False
        End Try

        RaiseSettingsChanged()
    End Sub

    Private Sub OnToolChanged(sender As Object, e As EventArgs)
        If _suppressEvents Then Return
        Dim tool = ToolFromComboIndex(_cmbTool.SelectedIndex)
        _settings.SelectTool(tool)
        UpdateControlsForTool(tool)
        SyncAppearanceControlsFromSettings()
        ' Picking a tool always enters Draw (ready to create with that tool)
        EnterDrawMode()
    End Sub

    Private Sub UpdateControlsForTool(tool As DrawingTool)
        Dim ink = DrawingHelper.IsInkTool(tool)
        Dim blur = (tool = DrawingTool.Blur)
        Dim text = (tool = DrawingTool.Text)
        ' Blur uses brush size only (no color/opacity)
        _lblColor.Visible = Not blur
        _btnColor.Visible = Not blur
        _lblOpacity.Visible = ink AndAlso Not blur
        _cmbOpacity.Visible = ink AndAlso Not blur
        _lblSize.Text = If(text, "Font:", "Size:")
        RebuildSizeChoices(forText:=text)
    End Sub

    Private Sub RebuildSizeChoices(forText As Boolean)
        Dim prev = _suppressEvents
        _suppressEvents = True
        Try
            Dim current = If(_cmbThickness.SelectedIndex >= 0 AndAlso _cmbThickness.SelectedIndex < _cmbThickness.Items.Count,
                _cmbThickness.SelectedIndex, 0)
            _cmbThickness.Items.Clear()
            If forText Then
                For Each t In FontSizeChoices
                    _cmbThickness.Items.Add($"{CInt(t)} pt")
                Next
            Else
                For Each t In ThicknessChoices
                    _cmbThickness.Items.Add($"{CInt(t)} px")
                Next
            End If
            If _cmbThickness.Items.Count > 0 Then
                _cmbThickness.SelectedIndex = Math.Min(current, _cmbThickness.Items.Count - 1)
            End If
        Finally
            _suppressEvents = prev
        End Try
    End Sub

    Private Sub SyncAppearanceControlsFromSettings()
        _suppressEvents = True
        Try
            UpdateColorSwatch()
            SelectOpacity(_settings.OpacityPercent)
            Dim forText = (_settings.Tool = DrawingTool.Text)
            RebuildSizeChoices(forText:=forText)
            SelectThickness(_settings.Thickness, forText:=forText)
        Finally
            _suppressEvents = False
        End Try
    End Sub

    Private Sub OnColorClick(sender As Object, e As EventArgs)
        Using dlg As New ColorDialog() With {
            .Color = _settings.BaseColor,
            .FullOpen = True,
            .AnyColor = True
        }
            If dlg.ShowDialog(FindForm()) = DialogResult.OK Then
                _settings.BaseColor = dlg.Color
                UpdateColorSwatch()
                NotifyAppearanceChanged()
            End If
        End Using
    End Sub

    Private Sub OnOpacityChanged(sender As Object, e As EventArgs)
        If _suppressEvents Then Return
        Dim idx = _cmbOpacity.SelectedIndex
        If idx >= 0 AndAlso idx < OpacityChoices.Length Then
            _settings.OpacityPercent = OpacityChoices(idx)
            NotifyAppearanceChanged()
        End If
    End Sub

    Private Sub OnThicknessChanged(sender As Object, e As EventArgs)
        If _suppressEvents Then Return
        Dim forText = (_settings.Tool = DrawingTool.Text)
        Dim choices = If(forText, FontSizeChoices, ThicknessChoices)
        Dim idx = _cmbThickness.SelectedIndex
        If idx >= 0 AndAlso idx < choices.Length Then
            _settings.Thickness = choices(idx)
            NotifyAppearanceChanged()
        End If
    End Sub

    ''' <summary>
    ''' Appearance edits while in Pointer stay in Pointer so a selected annotation can be restyled.
    ''' Always raises AppearanceChanged so Size/Color updates apply to the selection.
    ''' Tool switches do not call this (so they won't restyle the selection).
    ''' </summary>
    Private Sub NotifyAppearanceChanged()
        If Not _modeIsPointer Then
            EnterDrawMode()
        Else
            RaiseSettingsChanged()
        End If
        RaiseAppearanceChanged()
    End Sub

    Private Sub UpdateColorSwatch()
        _btnColor.BackColor = _settings.BaseColor
        _btnColor.ForeColor = _settings.BaseColor
        _btnColor.Text = "■■"
    End Sub

    Private Sub SelectOpacity(percent As Integer)
        Dim best = 0
        Dim bestDiff = Integer.MaxValue
        For i = 0 To OpacityChoices.Length - 1
            Dim d = Math.Abs(OpacityChoices(i) - percent)
            If d < bestDiff Then
                bestDiff = d
                best = i
            End If
        Next
        Dim prev = _suppressEvents
        _suppressEvents = True
        Try
            _cmbOpacity.SelectedIndex = best
            _settings.OpacityPercent = OpacityChoices(best)
        Finally
            _suppressEvents = prev
        End Try
    End Sub

    Private Sub SelectThickness(thickness As Single, Optional forText As Boolean = False)
        Dim choices = If(forText, FontSizeChoices, ThicknessChoices)
        Dim best = 0
        Dim bestDiff = Single.MaxValue
        For i = 0 To choices.Length - 1
            Dim d = Math.Abs(choices(i) - thickness)
            If d < bestDiff Then
                bestDiff = d
                best = i
            End If
        Next
        Dim prev = _suppressEvents
        _suppressEvents = True
        Try
            If best < _cmbThickness.Items.Count Then
                _cmbThickness.SelectedIndex = best
            End If
            _settings.Thickness = choices(best)
        Finally
            _suppressEvents = prev
        End Try
    End Sub

    Private Shared Function ToolFromComboIndex(index As Integer) As DrawingTool
        If index >= 0 AndAlso index < ToolOrder.Length Then
            Return ToolOrder(index)
        End If
        Return DrawingTool.Highlighter
    End Function
End Class
