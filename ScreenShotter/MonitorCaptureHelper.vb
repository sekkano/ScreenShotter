''' <summary>
''' Pure multi-monitor geometry helpers (no UI dependencies).
''' Virtual-screen coordinates: same space as Screen.Bounds / SystemInformation.VirtualScreen.
''' </summary>
Public Module MonitorCaptureHelper
    ''' <summary>
    ''' Converts a point from a monitor's local client space into virtual-screen space.
    ''' </summary>
    Public Function ToVirtualPoint(monitorBounds As Rectangle, localPoint As Point) As Point
        Return New Point(monitorBounds.X + localPoint.X, monitorBounds.Y + localPoint.Y)
    End Function

    ''' <summary>
    ''' Converts a virtual-screen point into a monitor's local client space.
    ''' </summary>
    Public Function ToLocalPoint(monitorBounds As Rectangle, virtualPoint As Point) As Point
        Return New Point(virtualPoint.X - monitorBounds.X, virtualPoint.Y - monitorBounds.Y)
    End Function

    ''' <summary>
    ''' Converts a virtual-screen rectangle into a monitor's local client space.
    ''' </summary>
    Public Function ToLocalRect(monitorBounds As Rectangle, virtualRect As Rectangle) As Rectangle
        Return New Rectangle(
            virtualRect.X - monitorBounds.X,
            virtualRect.Y - monitorBounds.Y,
            virtualRect.Width,
            virtualRect.Height)
    End Function

    ''' <summary>
    ''' Intersection of a virtual selection with one monitor, still in virtual coordinates.
    ''' </summary>
    Public Function IntersectVirtual(selectionVirtual As Rectangle, monitorBounds As Rectangle) As Rectangle
        Return Rectangle.Intersect(selectionVirtual, monitorBounds)
    End Function

    ''' <summary>
    ''' Destination rectangle inside a capture bitmap sized to <paramref name="selectionVirtual"/>
    ''' for the given virtual intersection (relative to selection origin).
    ''' </summary>
    Public Function DestinationInCapture(selectionVirtual As Rectangle, intersectionVirtual As Rectangle) As Rectangle
        Return New Rectangle(
            intersectionVirtual.X - selectionVirtual.X,
            intersectionVirtual.Y - selectionVirtual.Y,
            intersectionVirtual.Width,
            intersectionVirtual.Height)
    End Function

    ''' <summary>
    ''' Source rectangle inside a per-monitor bitmap (origin at monitor top-left).
    ''' </summary>
    Public Function SourceInMonitorBitmap(monitorBounds As Rectangle, intersectionVirtual As Rectangle) As Rectangle
        Return New Rectangle(
            intersectionVirtual.X - monitorBounds.X,
            intersectionVirtual.Y - monitorBounds.Y,
            intersectionVirtual.Width,
            intersectionVirtual.Height)
    End Function

    ''' <summary>
    ''' Inflates a rectangle for border/crosshair dirty regions; clamps to bounds when provided.
    ''' </summary>
    Public Function InflateForInvalidate(rect As Rectangle, amount As Integer, Optional clampTo As Rectangle? = Nothing) As Rectangle
        If rect.IsEmpty AndAlso rect.Width = 0 AndAlso rect.Height = 0 Then
            Return Rectangle.Empty
        End If
        Dim r = rect
        r.Inflate(amount, amount)
        If clampTo.HasValue Then
            r = Rectangle.Intersect(r, clampTo.Value)
        End If
        Return r
    End Function

    ''' <summary>
    ''' Union of two rectangles, treating empty (0-size at 0,0 unused) as absent when flagged.
    ''' </summary>
    Public Function UnionDirty(a As Rectangle, b As Rectangle) As Rectangle
        Dim aOk = a.Width > 0 OrElse a.Height > 0
        Dim bOk = b.Width > 0 OrElse b.Height > 0
        If aOk AndAlso bOk Then Return Rectangle.Union(a, b)
        If aOk Then Return a
        If bOk Then Return b
        Return Rectangle.Empty
    End Function

    ''' <summary>
    ''' Horizontal and vertical crosshair strips in local coordinates for a cursor local point.
    ''' </summary>
    Public Function CrosshairDirtyRegions(localCursor As Point, clientSize As Size, thickness As Integer) As Rectangle()
        Dim t = Math.Max(1, thickness)
        Dim half = t \ 2
        Dim h As New Rectangle(0, localCursor.Y - half, clientSize.Width, t)
        Dim v As New Rectangle(localCursor.X - half, 0, t, clientSize.Height)
        Return {h, v}
    End Function

    ''' <summary>
    ''' Builds the virtual-screen union of an arbitrary set of monitor bounds (any count).
    ''' </summary>
    Public Function UnionMonitorBounds(monitorBounds As IEnumerable(Of Rectangle)) As Rectangle
        Dim any = False
        Dim result As Rectangle = Rectangle.Empty
        For Each b In monitorBounds
            If Not any Then
                result = b
                any = True
            Else
                result = Rectangle.Union(result, b)
            End If
        Next
        Return result
    End Function

    ''' <summary>
    ''' True when the virtual selection intersects the monitor.
    ''' </summary>
    Public Function SelectionTouchesMonitor(selectionVirtual As Rectangle, monitorBounds As Rectangle) As Boolean
        If Not RegionHelper.IsValidCaptureRegion(selectionVirtual) Then Return False
        Return Not Rectangle.Intersect(selectionVirtual, monitorBounds).IsEmpty
    End Function
End Module
