''' <summary>
''' Pure helpers for AutoScroll document vs client coordinates.
''' Child.Location is in document space; PointToClient returns client space.
''' </summary>
Public Module CanvasCoordinateHelper
    ''' <summary>
    ''' Converts a point in the scrollable parent's client area into document coordinates
    ''' (same space as child Control.Location when AutoScroll is used).
    ''' </summary>
    ''' <param name="clientPoint">Point from PointToClient (visible client coords).</param>
    ''' <param name="autoScrollPosition">ScrollableControl.AutoScrollPosition (getter; often negative).</param>
    Public Function ClientToDocument(clientPoint As Point, autoScrollPosition As Point) As Point
        ' AutoScrollPosition getter is negative of scroll offset.
        Return New Point(
            clientPoint.X - autoScrollPosition.X,
            clientPoint.Y - autoScrollPosition.Y)
    End Function

    ''' <summary>
    ''' Converts document coordinates back to client coordinates.
    ''' </summary>
    Public Function DocumentToClient(documentPoint As Point, autoScrollPosition As Point) As Point
        Return New Point(
            documentPoint.X + autoScrollPosition.X,
            documentPoint.Y + autoScrollPosition.Y)
    End Function

    ''' <summary>
    ''' Minimum AutoScrollMinSize that can reach every frame (document space, non-negative origin).
    ''' </summary>
    Public Function ComputeScrollMinSize(
        frames As IEnumerable(Of Rectangle),
        clientSize As Size,
        Optional pad As Integer = 80) As Size

        Dim maxR = 0
        Dim maxB = 0
        Dim any = False
        For Each r In frames
            any = True
            maxR = Math.Max(maxR, r.Right)
            maxB = Math.Max(maxB, r.Bottom)
        Next
        If Not any Then
            Return Size.Empty
        End If
        Return New Size(
            Math.Max(clientSize.Width, maxR + pad),
            Math.Max(clientSize.Height, maxB + pad))
    End Function

    ''' <summary>
    ''' Clamps a child location so it stays in scrollable non-negative document space
    ''' (keeps at least a grip-sized portion reachable).
    ''' </summary>
    Public Function ClampDocumentLocation(location As Point, size As Size, Optional minVisible As Integer = 40) As Point
        Dim x = location.X
        Dim y = location.Y
        ' Do not allow fully off into negative document space (AutoScroll cannot reach it)
        x = Math.Max(0, x)
        y = Math.Max(0, y)
        Return New Point(x, y)
    End Function
End Module
