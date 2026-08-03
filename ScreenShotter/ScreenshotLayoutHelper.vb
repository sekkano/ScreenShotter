''' <summary>
''' Pure layout helpers for placing new screenshots without stacking on top of each other.
''' Prefers the visible window width so new snips land beside existing ones only when they
''' still start on-screen; otherwise wraps to a new row below.
''' </summary>
Public Module ScreenshotLayoutHelper
    Public Const DefaultGap As Integer = 16
    Public Const DefaultMargin As Integer = 20
    ''' <summary>Fallback when no viewport width is supplied (tests / headless).</summary>
    Public Const DefaultPreferredRowWidth As Integer = 1600
    ''' <summary>Minimum left-edge strip that must stay inside the viewport to place beside.</summary>
    Public Const DefaultMinVisibleEdge As Integer = 48

    ''' <summary>
    ''' Chooses a top-left for a new screenshot:
    ''' prefer to the right of the previous one when its left edge still falls inside the
    ''' current viewport width; otherwise wrap to a new row under all existing frames,
    ''' left-aligned to the viewport so the snip is visible without horizontal scrolling.
    ''' </summary>
    ''' <param name="existingFrames">Frames already on the canvas (document coordinates).</param>
    ''' <param name="newSize">Natural size of the incoming screenshot.</param>
    ''' <param name="viewportOrigin">Top-left of the visible area in document space (scroll offset).</param>
    ''' <param name="viewportWidth">Visible canvas width in pixels; drives wrap decisions.</param>
    ''' <param name="gap">Space between screenshots.</param>
    ''' <param name="minVisibleEdge">How much of the new snip's left edge must stay in view to place beside.</param>
    Public Function PlaceNextScreenshot(
        existingFrames As IReadOnlyList(Of Rectangle),
        newSize As Size,
        Optional viewportOrigin As Point = Nothing,
        Optional viewportWidth As Integer = 0,
        Optional gap As Integer = DefaultGap,
        Optional minVisibleEdge As Integer = DefaultMinVisibleEdge) As Point

        Dim viewW = If(viewportWidth > 0, viewportWidth, DefaultPreferredRowWidth)
        Dim baseX = viewportOrigin.X + DefaultMargin
        Dim baseY = viewportOrigin.Y + DefaultMargin
        Dim viewRight = viewportOrigin.X + viewW

        If existingFrames Is Nothing OrElse existingFrames.Count = 0 Then
            Return New Point(baseX, baseY)
        End If

        Dim last = existingFrames(existingFrames.Count - 1)
        Dim toTheRight As New Point(last.Right + gap, last.Top)

        ' Place beside the last snip only when the new left edge is still inside the window.
        ' The image may extend past the right edge — that is fine; no horizontal scroll needed
        ' just to find the new capture.
        Dim edge = Math.Max(1, Math.Min(minVisibleEdge, Math.Max(1, newSize.Width)))
        If toTheRight.X + edge <= viewRight Then
            Return toTheRight
        End If

        ' New row under everything already placed, aligned to the visible left margin
        Dim maxBottom = existingFrames(0).Bottom
        For Each r In existingFrames
            If r.Bottom > maxBottom Then maxBottom = r.Bottom
        Next
        Return New Point(baseX, maxBottom + gap)
    End Function

    ''' <summary>
    ''' Legacy overload kept for callers/tests that pass a fixed preferred row width
    ''' instead of a live viewport (treated as width from document X=0).
    ''' </summary>
    Public Function PlaceNextScreenshot(
        existingFrames As IReadOnlyList(Of Rectangle),
        newSize As Size,
        origin As Point,
        gap As Integer,
        preferredRowWidth As Integer) As Point

        Return PlaceNextScreenshot(
            existingFrames,
            newSize,
            viewportOrigin:=origin,
            viewportWidth:=preferredRowWidth,
            gap:=gap)
    End Function
End Module
