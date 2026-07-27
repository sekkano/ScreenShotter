''' <summary>
''' Annotation toolbar under the tab headers: Pointer, drawing-tool dropdown,
''' color, transparency, and thickness.
''' </summary>
Public Class DrawingToolStrip
    Inherits ClickThroughToolStrip

    Private ReadOnly _btnPointer As ToolStripButton
    Private ReadOnly _cmbTool As ToolStripComboBox
    Private ReadOnly _btnColor As ToolStripButton
    Private ReadOnly _cmbOpacity As ToolStripComboBox
    Private ReadOnly _cmbThickness As ToolStripComboBox
    Private ReadOnly _settings As New DrawingSettings()
    Private _modeIsPointer As Boolean = True
    Private _suppressEvents As Boolean

    Public Event SettingsChanged As EventHandler

    Private Shared ReadOnly OpacityChoices As Integer() = {20, 30, 40, 50, 60, 70, 80, 90, 100}
    Private Shared ReadOnly ThicknessChoices As Single() = {2, 4, 8, 12, 16, 20, 28, 36, 48, 64}

    Public Sub New()
        MyBase.New()
        GripStyle = ToolStripGripStyle.Hidden
        Dock = DockStyle.Top
        Padding = New Padding(4, 2, 4, 2)
        BackColor = Color.FromArgb(250, 250, 252)

        _btnPointer = New ToolStripButton("Pointer") With {
            .CheckOnClick = True,
            .Checked = True,
            .DisplayStyle = ToolStripItemDisplayStyle.Text,
            .ToolTipText = "Select and move screenshots (or use Ctrl+drag while drawing)"
        }

        _cmbTool = New ToolStripComboBox("cmbTool") With {
            .DropDownStyle = ComboBoxStyle.DropDownList,
            .AutoSize = False,
            .Width = 110,
            .ToolTipText = "Drawing tool"
        }
        _cmbTool.Items.AddRange(New Object() {
            DrawingHelper.ToolDisplayName(DrawingTool.Highlighter),
            DrawingHelper.ToolDisplayName(DrawingTool.Pen)
        })
        _cmbTool.SelectedIndex = 0

        _btnColor = New ToolStripButton("  ") With {
            .DisplayStyle = ToolStripItemDisplayStyle.Text,
            .ToolTipText = "Ink color",
            .AutoSize = False,
            .Width = 36
        }
        UpdateColorSwatch()

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

        _cmbThickness = New ToolStripComboBox("cmbThickness") With {
            .DropDownStyle = ComboBoxStyle.DropDownList,
            .AutoSize = False,
            .Width = 72,
            .ToolTipText = "Stroke thickness"
        }
        For Each t In ThicknessChoices
            _cmbThickness.Items.Add($"{CInt(t)} px")
        Next
        SelectThickness(DrawingHelper.DefaultThickness)

        Items.Add(_btnPointer)
        Items.Add(New ToolStripSeparator())
        Items.Add(New ToolStripLabel("Draw:") With {.ForeColor = Color.DimGray})
        Items.Add(_cmbTool)
        Items.Add(New ToolStripLabel("Color:") With {.ForeColor = Color.DimGray})
        Items.Add(_btnColor)
        Items.Add(New ToolStripLabel("Opacity:") With {.ForeColor = Color.DimGray})
        Items.Add(_cmbOpacity)
        Items.Add(New ToolStripLabel("Size:") With {.ForeColor = Color.DimGray})
        Items.Add(_cmbThickness)

        AddHandler _btnPointer.Click, AddressOf OnPointerClick
        AddHandler _cmbTool.SelectedIndexChanged, AddressOf OnToolChanged
        AddHandler _btnColor.Click, AddressOf OnColorClick
        AddHandler _cmbOpacity.SelectedIndexChanged, AddressOf OnOpacityChanged
        AddHandler _cmbThickness.SelectedIndexChanged, AddressOf OnThicknessChanged
        ' Opening or focusing the draw list switches into draw mode (even if tool unchanged)
        AddHandler _cmbTool.DropDown, AddressOf OnDrawUiActivated
        AddHandler _cmbTool.Click, AddressOf OnDrawUiActivated
        AddHandler _cmbOpacity.DropDown, AddressOf OnDrawUiActivated
        AddHandler _cmbThickness.DropDown, AddressOf OnDrawUiActivated
    End Sub

    Private Sub OnDrawUiActivated(sender As Object, e As EventArgs)
        If _suppressEvents Then Return
        If _modeIsPointer Then
            _modeIsPointer = False
            _btnPointer.Checked = False
            RaiseSettingsChanged()
        End If
    End Sub

    ''' <summary>Current tool for the canvas: Pointer or a draw tool.</summary>
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

    Private Sub OnPointerClick(sender As Object, e As EventArgs)
        _modeIsPointer = True
        _btnPointer.Checked = True
        RaiseSettingsChanged()
    End Sub

    Private Sub OnToolChanged(sender As Object, e As EventArgs)
        If _suppressEvents Then Return
        Dim tool = ToolFromComboIndex(_cmbTool.SelectedIndex)
        ' Switch tool only — restore that tool's last color/opacity/size
        _settings.SelectTool(tool)
        SyncAppearanceControlsFromSettings()
        ' Selecting a drawing tool switches out of pure Pointer mode
        _modeIsPointer = False
        _btnPointer.Checked = False
        RaiseSettingsChanged()
    End Sub

    Private Sub SyncAppearanceControlsFromSettings()
        _suppressEvents = True
        Try
            UpdateColorSwatch()
            SelectOpacity(_settings.OpacityPercent)
            SelectThickness(_settings.Thickness)
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
                ' Color pick implies user wants to draw
                _modeIsPointer = False
                _btnPointer.Checked = False
                RaiseSettingsChanged()
            End If
        End Using
    End Sub

    Private Sub OnOpacityChanged(sender As Object, e As EventArgs)
        If _suppressEvents Then Return
        Dim idx = _cmbOpacity.SelectedIndex
        If idx >= 0 AndAlso idx < OpacityChoices.Length Then
            _settings.OpacityPercent = OpacityChoices(idx)
            RaiseSettingsChanged()
        End If
    End Sub

    Private Sub OnThicknessChanged(sender As Object, e As EventArgs)
        If _suppressEvents Then Return
        Dim idx = _cmbThickness.SelectedIndex
        If idx >= 0 AndAlso idx < ThicknessChoices.Length Then
            _settings.Thickness = ThicknessChoices(idx)
            RaiseSettingsChanged()
        End If
    End Sub

    Private Sub UpdateColorSwatch()
        Dim c = _settings.StrokeColor
        ' Solid swatch of base color; opacity shown separately
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

    Private Sub SelectThickness(thickness As Single)
        Dim best = 0
        Dim bestDiff = Single.MaxValue
        For i = 0 To ThicknessChoices.Length - 1
            Dim d = Math.Abs(ThicknessChoices(i) - thickness)
            If d < bestDiff Then
                bestDiff = d
                best = i
            End If
        Next
        Dim prev = _suppressEvents
        _suppressEvents = True
        Try
            _cmbThickness.SelectedIndex = best
            _settings.Thickness = ThicknessChoices(best)
        Finally
            _suppressEvents = prev
        End Try
    End Sub

    Private Shared Function ToolFromComboIndex(index As Integer) As DrawingTool
        Select Case index
            Case 1 : Return DrawingTool.Pen
            Case Else : Return DrawingTool.Highlighter
        End Select
    End Function
End Class
