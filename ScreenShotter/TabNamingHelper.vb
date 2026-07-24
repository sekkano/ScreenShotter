''' <summary>
''' Pure helpers for default tab titles (no UI).
''' </summary>
Public Module TabNamingHelper
    ''' <summary>
    ''' Default title from how many tabs already exist (0 → "Tab 1", 1 → "Tab 2", …).
    ''' </summary>
    Public Function NextDefaultTabName(existingTabCount As Integer) As String
        Dim n = Math.Max(0, existingTabCount)
        Return $"Tab {n + 1}"
    End Function

    ''' <summary>
    ''' Trims a rename; returns Nothing if blank/cancelled.
    ''' </summary>
    Public Function NormalizeTabName(proposed As String) As String
        If proposed Is Nothing Then Return Nothing
        Dim t = proposed.Trim()
        If t.Length = 0 Then Return Nothing
        Return t
    End Function
End Module
