''' <summary>
''' Pure geometry helpers for compositing a tab's screenshots into one export image.
''' </summary>
Public Module TabExportHelper
    ''' <summary>
    ''' Bounding rectangle of all layer frames in canvas coordinates.
    ''' Empty if no layers.
    ''' </summary>
    Public Function ComputeUnionBounds(frames As IEnumerable(Of Rectangle)) As Rectangle
        Dim any = False
        Dim result As Rectangle = Rectangle.Empty
        For Each r In frames
            If r.Width <= 0 OrElse r.Height <= 0 Then Continue For
            If Not any Then
                result = r
                any = True
            Else
                result = Rectangle.Union(result, r)
            End If
        Next
        Return result
    End Function

    ''' <summary>
    ''' Destination rectangle for a frame inside the export bitmap (origin at union top-left).
    ''' </summary>
    Public Function FrameInExport(unionBounds As Rectangle, frame As Rectangle) As Rectangle
        Return New Rectangle(
            frame.X - unionBounds.X,
            frame.Y - unionBounds.Y,
            frame.Width,
            frame.Height)
    End Function

    ''' <summary>
    ''' Picks an ImageFormat from a file path extension (defaults to PNG).
    ''' </summary>
    Public Function FormatFromPath(path As String) As Imaging.ImageFormat
        If String.IsNullOrWhiteSpace(path) Then
            Return Imaging.ImageFormat.Png
        End If
        Dim ext = IO.Path.GetExtension(path).ToLowerInvariant()
        Select Case ext
            Case ".jpg", ".jpeg"
                Return Imaging.ImageFormat.Jpeg
            Case ".bmp"
                Return Imaging.ImageFormat.Bmp
            Case ".gif"
                Return Imaging.ImageFormat.Gif
            Case Else
                Return Imaging.ImageFormat.Png
        End Select
    End Function

    ''' <summary>
    ''' True when a path is acceptable for saving an image.
    ''' </summary>
    Public Function IsValidSavePath(path As String) As Boolean
        If String.IsNullOrWhiteSpace(path) Then Return False
        Try
            Dim full = IO.Path.GetFullPath(path)
            Dim name = IO.Path.GetFileName(full)
            Return Not String.IsNullOrWhiteSpace(name)
        Catch
            Return False
        End Try
    End Function

    ''' <summary>
    ''' WinForms z-order: Controls(0) is the front (top-most). Bitmap compositing must
    ''' paint back-to-front so the top-most layer is drawn last (visible on top).
    ''' Returns indices: Count-1, Count-2, …, 0.
    ''' </summary>
    Public Function BottomToTopControlIndices(controlCount As Integer) As Integer()
        If controlCount <= 0 Then Return Array.Empty(Of Integer)()
        Dim result(controlCount - 1) As Integer
        For i = 0 To controlCount - 1
            result(i) = controlCount - 1 - i
        Next
        Return result
    End Function
End Module
