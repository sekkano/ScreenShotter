''' <summary>
''' Pure zoom / viewport / resize math (no UI dependencies).
''' Zoom scales image content inside a fixed frame; frame size is independent.
''' </summary>
Public Module ZoomHelper
    Public Const MinZoom As Double = 0.05
    Public Const MaxZoom As Double = 8.0
    Public Const DefaultZoom As Double = 1.0
    Public Const ZoomStepFactor As Double = 1.15

    Public Function ClampZoom(zoom As Double) As Double
        If Double.IsNaN(zoom) OrElse Double.IsInfinity(zoom) Then
            Return DefaultZoom
        End If
        Return Math.Min(MaxZoom, Math.Max(MinZoom, zoom))
    End Function

    ''' <summary>
    ''' Size of the zoomed image content when the frame is filled at zoom 1.0.
    ''' Content grows with zoom; the frame (viewport) does not.
    ''' </summary>
    Public Function ContentSize(frame As Size, zoom As Double) As Size
        Dim z = ClampZoom(zoom)
        Dim w = Math.Max(1, CInt(Math.Round(frame.Width * z)))
        Dim h = Math.Max(1, CInt(Math.Round(frame.Height * z)))
        Return New Size(w, h)
    End Function

    ''' <summary>
    ''' Native-pixel size at a zoom factor (legacy/helper for tests and 100% sizing).
    ''' </summary>
    Public Function DisplaySize(natural As Size, zoom As Double) As Size
        Dim z = ClampZoom(zoom)
        Dim w = Math.Max(1, CInt(Math.Round(natural.Width * z)))
        Dim h = Math.Max(1, CInt(Math.Round(natural.Height * z)))
        Return New Size(w, h)
    End Function

    ''' <summary>
    ''' Multiplies zoom by stepFactor^steps (positive = zoom in).
    ''' </summary>
    Public Function ZoomBySteps(currentZoom As Double, steps As Integer, Optional stepFactor As Double = ZoomStepFactor) As Double
        If steps = 0 Then Return ClampZoom(currentZoom)
        Dim factor = If(stepFactor <= 1.0, ZoomStepFactor, stepFactor)
        Dim z = ClampZoom(currentZoom)
        If steps > 0 Then
            For i = 1 To steps
                z *= factor
            Next
        Else
            For i = 1 To -steps
                z /= factor
            Next
        End If
        Return ClampZoom(z)
    End Function

    Public Function ZoomFromDisplayWidth(naturalWidth As Integer, displayWidth As Integer) As Double
        If naturalWidth <= 0 Then Return DefaultZoom
        Return ClampZoom(displayWidth / CDbl(naturalWidth))
    End Function

    ''' <summary>
    ''' Aspect-preserving size for diagonal (corner) resize against a reference aspect size.
    ''' </summary>
    Public Function AspectPreserveSize(reference As Size, tentative As Size) As Size
        If reference.Width <= 0 OrElse reference.Height <= 0 Then
            Return New Size(Math.Max(1, tentative.Width), Math.Max(1, tentative.Height))
        End If
        Dim sx = tentative.Width / CDbl(reference.Width)
        Dim sy = tentative.Height / CDbl(reference.Height)
        ' Dominant axis so the dragged corner tracks the pointer closely
        Dim scale = If(Math.Abs(sx - 1.0) >= Math.Abs(sy - 1.0), sx, sy)
        If scale <= 0 Then scale = 0.01
        Dim w = Math.Max(1, CInt(Math.Round(reference.Width * scale)))
        Dim h = Math.Max(1, CInt(Math.Round(reference.Height * scale)))
        Return New Size(w, h)
    End Function

    ''' <summary>
    ''' Free (non-uniform) size clamp for edge resize.
    ''' </summary>
    Public Function FreeResizeSize(tentative As Size, minPx As Integer) As Size
        Return New Size(Math.Max(minPx, tentative.Width), Math.Max(minPx, tentative.Height))
    End Function

    ''' <summary>
    ''' Clamps pan so the content always covers the viewport when larger; centers when smaller.
    ''' Pan is the top-left of the content relative to the viewport (negative or zero when zoomed in).
    ''' </summary>
    Public Function ClampPan(pan As Point, content As Size, viewport As Size) As Point
        Dim x = pan.X
        Dim y = pan.Y

        If content.Width <= viewport.Width Then
            x = (viewport.Width - content.Width) \ 2
        Else
            Dim minX = viewport.Width - content.Width
            x = Math.Min(0, Math.Max(minX, x))
        End If

        If content.Height <= viewport.Height Then
            y = (viewport.Height - content.Height) \ 2
        Else
            Dim minY = viewport.Height - content.Height
            y = Math.Min(0, Math.Max(minY, y))
        End If

        Return New Point(x, y)
    End Function

    ''' <summary>
    ''' Adjusts pan so the content point under the cursor stays fixed after a zoom change.
    ''' </summary>
    Public Function PanAfterZoom(oldZoom As Double, newZoom As Double, oldPan As Point,
                                 cursorInViewport As Point, frame As Size) As Point
        Dim oz = ClampZoom(oldZoom)
        Dim nz = ClampZoom(newZoom)
        If Math.Abs(oz - nz) < 0.0000001 OrElse oz <= 0 Then
            Return oldPan
        End If

        ' Content coordinate under cursor before zoom
        Dim contentX = (cursorInViewport.X - oldPan.X) / oz
        Dim contentY = (cursorInViewport.Y - oldPan.Y) / oz

        ' New pan so that point stays under cursor
        Dim newPan As New Point(
            CInt(Math.Round(cursorInViewport.X - contentX * nz)),
            CInt(Math.Round(cursorInViewport.Y - contentY * nz)))

        Dim content = ContentSize(frame, nz)
        Return ClampPan(newPan, content, frame)
    End Function

    Public Function FormatZoomPercent(zoom As Double) As String
        Return $"{CInt(Math.Round(ClampZoom(zoom) * 100))}%"
    End Function

    Public Function IsCornerEdge(edgeName As String) As Boolean
        Select Case edgeName
            Case "NE", "NW", "SE", "SW" : Return True
            Case Else : Return False
        End Select
    End Function
End Module
