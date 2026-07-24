<Global.Microsoft.VisualBasic.CompilerServices.DesignerGenerated()>
Partial Class frmScreenShotter
    Inherits System.Windows.Forms.Form

    'Form overrides dispose to clean up the component list.
    <System.Diagnostics.DebuggerNonUserCode()>
    Protected Overrides Sub Dispose(disposing As Boolean)
        Try
            If disposing AndAlso components IsNot Nothing Then
                components.Dispose()
            End If
        Finally
            MyBase.Dispose(disposing)
        End Try
    End Sub

    'Required by the Windows Form Designer
    Private components As System.ComponentModel.IContainer

    'NOTE: The following procedure is required by the Windows Form Designer
    'It can be modified using the Windows Form Designer.  
    'Do not modify it using the code editor.
    <System.Diagnostics.DebuggerStepThrough()>
    Private Sub InitializeComponent()
        toolStrip = New ToolStrip()
        btnCapture = New ToolStripButton()
        toolStripSeparator1 = New ToolStripSeparator()
        btnNewTab = New ToolStripButton()
        btnCloseTab = New ToolStripButton()
        toolStripSeparator2 = New ToolStripSeparator()
        btnZoomOut = New ToolStripButton()
        btnZoomReset = New ToolStripButton()
        btnZoomIn = New ToolStripButton()
        statusStrip = New StatusStrip()
        statusLabel = New ToolStripStatusLabel()
        tabControl = New TabControl()
        toolStrip.SuspendLayout()
        statusStrip.SuspendLayout()
        SuspendLayout()
        '
        'toolStrip
        '
        toolStrip.GripStyle = ToolStripGripStyle.Hidden
        toolStrip.Items.AddRange(New ToolStripItem() {
            btnCapture, toolStripSeparator1, btnNewTab, btnCloseTab,
            toolStripSeparator2, btnZoomOut, btnZoomReset, btnZoomIn})
        toolStrip.Location = New Point(0, 0)
        toolStrip.Name = "toolStrip"
        toolStrip.Padding = New Padding(6, 2, 6, 2)
        toolStrip.Size = New Size(1000, 28)
        toolStrip.TabIndex = 0
        toolStrip.Text = "toolStrip"
        '
        'btnCapture
        '
        btnCapture.DisplayStyle = ToolStripItemDisplayStyle.Text
        btnCapture.Font = New Font("Segoe UI", 9.0F, FontStyle.Bold)
        btnCapture.Name = "btnCapture"
        btnCapture.Size = New Size(100, 23)
        btnCapture.Text = "New Screenshot"
        btnCapture.ToolTipText = "Capture a rectangular region (minimizes this window)"
        '
        'toolStripSeparator1
        '
        toolStripSeparator1.Name = "toolStripSeparator1"
        toolStripSeparator1.Size = New Size(6, 26)
        '
        'btnNewTab
        '
        btnNewTab.DisplayStyle = ToolStripItemDisplayStyle.Text
        btnNewTab.Name = "btnNewTab"
        btnNewTab.Size = New Size(61, 23)
        btnNewTab.Text = "New Tab"
        btnNewTab.ToolTipText = "Open a new tab for independent screenshots"
        '
        'btnCloseTab
        '
        btnCloseTab.DisplayStyle = ToolStripItemDisplayStyle.Text
        btnCloseTab.Name = "btnCloseTab"
        btnCloseTab.Size = New Size(68, 23)
        btnCloseTab.Text = "Close Tab"
        btnCloseTab.ToolTipText = "Close the active tab"
        '
        'toolStripSeparator2
        '
        toolStripSeparator2.Name = "toolStripSeparator2"
        toolStripSeparator2.Size = New Size(6, 26)
        '
        'btnZoomOut
        '
        btnZoomOut.DisplayStyle = ToolStripItemDisplayStyle.Text
        btnZoomOut.Name = "btnZoomOut"
        btnZoomOut.Size = New Size(32, 23)
        btnZoomOut.Text = "−"
        btnZoomOut.ToolTipText = "Zoom out selected screenshot (Ctrl+Wheel)"
        '
        'btnZoomReset
        '
        btnZoomReset.DisplayStyle = ToolStripItemDisplayStyle.Text
        btnZoomReset.Name = "btnZoomReset"
        btnZoomReset.Size = New Size(44, 23)
        btnZoomReset.Text = "100%"
        btnZoomReset.ToolTipText = "Reset selected screenshot to full native size"
        '
        'btnZoomIn
        '
        btnZoomIn.DisplayStyle = ToolStripItemDisplayStyle.Text
        btnZoomIn.Name = "btnZoomIn"
        btnZoomIn.Size = New Size(32, 23)
        btnZoomIn.Text = "+"
        btnZoomIn.ToolTipText = "Zoom in selected screenshot (Ctrl+Wheel)"
        '
        'statusStrip
        '
        statusStrip.Items.AddRange(New ToolStripItem() {statusLabel})
        statusStrip.Location = New Point(0, 528)
        statusStrip.Name = "statusStrip"
        statusStrip.Size = New Size(1000, 22)
        statusStrip.TabIndex = 2
        statusStrip.Text = "statusStrip"
        '
        'statusLabel
        '
        statusLabel.Name = "statusLabel"
        statusLabel.Size = New Size(185, 17)
        statusLabel.Spring = True
        statusLabel.Text = "Ready — click New Screenshot to capture"
        statusLabel.TextAlign = ContentAlignment.MiddleLeft
        '
        'tabControl
        '
        tabControl.Dock = DockStyle.Fill
        tabControl.Location = New Point(0, 28)
        tabControl.Name = "tabControl"
        tabControl.SelectedIndex = 0
        tabControl.Size = New Size(1000, 500)
        tabControl.TabIndex = 1
        '
        'frmScreenShotter
        '
        AutoScaleDimensions = New SizeF(7.0F, 15.0F)
        AutoScaleMode = AutoScaleMode.Font
        ClientSize = New Size(1000, 550)
        Controls.Add(tabControl)
        Controls.Add(statusStrip)
        Controls.Add(toolStrip)
        MinimumSize = New Size(480, 320)
        Name = "frmScreenShotter"
        StartPosition = FormStartPosition.CenterScreen
        Text = "Screen Shotter"
        toolStrip.ResumeLayout(False)
        toolStrip.PerformLayout()
        statusStrip.ResumeLayout(False)
        statusStrip.PerformLayout()
        ResumeLayout(False)
        PerformLayout()
    End Sub

    Friend WithEvents toolStrip As ToolStrip
    Friend WithEvents btnCapture As ToolStripButton
    Friend WithEvents toolStripSeparator1 As ToolStripSeparator
    Friend WithEvents btnNewTab As ToolStripButton
    Friend WithEvents btnCloseTab As ToolStripButton
    Friend WithEvents toolStripSeparator2 As ToolStripSeparator
    Friend WithEvents btnZoomOut As ToolStripButton
    Friend WithEvents btnZoomReset As ToolStripButton
    Friend WithEvents btnZoomIn As ToolStripButton
    Friend WithEvents statusStrip As StatusStrip
    Friend WithEvents statusLabel As ToolStripStatusLabel
    Friend WithEvents tabControl As TabControl
End Class
