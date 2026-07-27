''' <summary>
''' Annotation toolbar that sits under the tab headers (top of each tab page).
''' Starts with Pointer + Highlighter.
''' </summary>
Public Class DrawingToolStrip
    Inherits ClickThroughToolStrip

    Private ReadOnly _btnPointer As ToolStripButton
    Private ReadOnly _btnHighlighter As ToolStripButton
    Private _activeTool As DrawingTool = DrawingTool.Pointer

    Public Event ActiveToolChanged As EventHandler

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
            .ToolTipText = "Select and move screenshots"
        }
        _btnHighlighter = New ToolStripButton("Highlighter") With {
            .CheckOnClick = True,
            .Checked = False,
            .DisplayStyle = ToolStripItemDisplayStyle.Text,
            .ToolTipText = "Draw a translucent yellow highlight on a screenshot"
        }

        Items.Add(_btnPointer)
        Items.Add(New ToolStripSeparator())
        Items.Add(_btnHighlighter)

        AddHandler _btnPointer.Click, AddressOf OnPointerClick
        AddHandler _btnHighlighter.Click, AddressOf OnHighlighterClick
    End Sub

    <System.ComponentModel.Browsable(False)>
    <System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)>
    Public Property ActiveTool As DrawingTool
        Get
            Return _activeTool
        End Get
        Set(value As DrawingTool)
            If _activeTool = value Then
                SyncButtons()
                Return
            End If
            _activeTool = value
            SyncButtons()
            RaiseEvent ActiveToolChanged(Me, EventArgs.Empty)
        End Set
    End Property

    Private Sub OnPointerClick(sender As Object, e As EventArgs)
        ActiveTool = DrawingTool.Pointer
    End Sub

    Private Sub OnHighlighterClick(sender As Object, e As EventArgs)
        ActiveTool = DrawingTool.Highlighter
    End Sub

    Private Sub SyncButtons()
        _btnPointer.Checked = (_activeTool = DrawingTool.Pointer)
        _btnHighlighter.Checked = (_activeTool = DrawingTool.Highlighter)
    End Sub
End Class
