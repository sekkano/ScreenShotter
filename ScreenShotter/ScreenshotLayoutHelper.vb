''' <summary>
''' Pure layout helpers for placing new screenshots without stacking on top of each other.
''' </summary>
Public Module ScreenshotLayoutHelper
    Public Const DefaultGap As Integer = 16
    Public Const DefaultMargin As Integer = 20
    Public Const DefaultPreferredRowWidth As Integer = 1600

    ''' <summary>
    ''' Chooses a top-left for a new screenshot: prefer to the right of the previous one;
    ''' wrap to a new row below all existing frames when the row would get too wide.
    ''' </summary>
    Public Function PlaceNextScreenshot(
        existingFrames As IReadOnlyList(Of Rectangle),
        newSize As Size,
        Optional origin As Point = Nothing,
        Optional gap As Integer = DefaultGap,
        Optional preferredRowWidth As Integer = DefaultPreferredRowWidth) As Point

        Dim baseX = origin.X + DefaultMargin
        Dim baseY = origin.Y + DefaultMargin

        If existingFrames Is Nothing OrElse existingFrames.Count = 0 Then
            Return New Point(baseX, baseY)
        End If

        Dim last = existingFrames(existingFrames.Count - 1)
        Dim toTheRight As New Point(last.Right + gap, last.Top)
        Dim rowLimit = baseX + Math.Max(preferredRowWidth, last.Width + gap + Math.Max(1, newSize.Width))

        ' Stay on the same row when the new frame still fits beside the last one
        If toTheRight.X + Math.Max(1, newSize.Width) <= rowLimit Then
            Return toTheRight
        End If

        ' New row under everything already placed
        Dim maxBottom = existingFrames(0).Bottom
        For Each r In existingFrames
            If r.Bottom > maxBottom Then maxBottom = r.Bottom
        Next
        Return New Point(baseX, maxBottom + gap)
    End Function
End Module
