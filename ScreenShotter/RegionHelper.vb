''' <summary>
''' Pure geometry helpers for capture region selection (no UI dependencies).
''' </summary>
Public Module RegionHelper
    ''' <summary>
    ''' Builds a normalized rectangle from any drag direction (including reverse drags).
    ''' </summary>
    Public Function NormalizeRect(x1 As Integer, y1 As Integer, x2 As Integer, y2 As Integer) As Rectangle
        Dim left = Math.Min(x1, x2)
        Dim top = Math.Min(y1, y2)
        Dim width = Math.Abs(x2 - x1)
        Dim height = Math.Abs(y2 - y1)
        Return New Rectangle(left, top, width, height)
    End Function

    ''' <summary>
    ''' Overload accepting two points (start and end of a drag).
    ''' </summary>
    Public Function NormalizeRect(startPoint As Point, endPoint As Point) As Rectangle
        Return NormalizeRect(startPoint.X, startPoint.Y, endPoint.X, endPoint.Y)
    End Function

    ''' <summary>
    ''' Returns True when the region has positive area suitable for capture.
    ''' </summary>
    Public Function IsValidCaptureRegion(region As Rectangle) As Boolean
        Return region.Width > 0 AndAlso region.Height > 0
    End Function
End Module
