''' <summary>
''' Pure helpers for mouse-wheel / horizontal-wheel scroll math (no UI dependencies).
''' </summary>
Public Module WheelScrollHelper
    Public Const WheelDeltaUnit As Integer = 120
    Public Const DefaultLinePixels As Integer = 48

    ''' <summary>
    ''' Extracts the signed wheel delta from a WM_MOUSEWHEEL / WM_MOUSEHWHEEL wParam.
    ''' </summary>
    Public Function DeltaFromWParam(wParam As IntPtr) As Integer
        ' HIWORD of the low 32 bits is a signed 16-bit value (WHEEL_DELTA units).
        Dim low32 As Long = wParam.ToInt64() And &HFFFFFFFFL
        Dim hi As Integer = CInt((low32 >> 16) And &HFFFFL)
        If hi >= &H8000 Then
            hi -= &H10000
        End If
        Return hi
    End Function

    ''' <summary>
    ''' Converts a wheel delta into a pixel scroll offset (positive delta → positive offset direction).
    ''' </summary>
    Public Function DeltaToScrollPixels(delta As Integer, Optional linePixels As Integer = DefaultLinePixels) As Integer
        If delta = 0 Then Return 0
        Dim lines = delta / CDbl(WheelDeltaUnit)
        Return CInt(Math.Round(lines * linePixels))
    End Function

    ''' <summary>
    ''' Computes the next AutoScrollPosition setter value (positive coords) from the
    ''' current getter value (typically negative) plus pixel deltas.
    ''' </summary>
    Public Function NextScrollPosition(currentAutoScrollPosition As Point,
                                       deltaXPixels As Integer,
                                       deltaYPixels As Integer) As Point
        ' Getter uses negative offsets; setter wants non-negative scroll amounts.
        Dim x = -currentAutoScrollPosition.X + deltaXPixels
        Dim y = -currentAutoScrollPosition.Y + deltaYPixels
        If x < 0 Then x = 0
        If y < 0 Then y = 0
        Return New Point(x, y)
    End Function

    ''' <summary>
    ''' True when the control under the cursor is a screenshot image (or child of one).
    ''' Used so canvas scrolling only happens on empty canvas.
    ''' </summary>
    Public Function IsScreenshotImageControl(ctrl As Control) As Boolean
        Dim c = ctrl
        While c IsNot Nothing
            If TypeOf c Is MovableScreenshotBox Then Return True
            c = c.Parent
        End While
        Return False
    End Function
End Module
