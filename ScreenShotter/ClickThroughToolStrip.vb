''' <summary>
''' ToolStrip that does not swallow the mouse message that activates the parent form.
''' Allows a single click on a toolbar button when the main window was inactive.
''' </summary>
Public Class ClickThroughToolStrip
    Inherits ToolStrip

    Public Sub New()
        MyBase.New()
    End Sub

    Protected Overrides Sub WndProc(ByRef m As Message)
        MyBase.WndProc(m)
        MouseActivateHelper.ApplyNoEatResult(m)
    End Sub
End Class
