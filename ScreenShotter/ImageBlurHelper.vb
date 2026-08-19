Imports System.Drawing.Drawing2D
Imports System.Drawing.Imaging

''' <summary>
''' Fast approximate blur via downscale / upscale (good enough for privacy redaction).
''' </summary>
Public Module ImageBlurHelper
    Public Const DefaultFactor As Integer = 12

    ''' <summary>
    ''' Returns a full-size blurred copy of <paramref name="source"/>. Caller owns the bitmap.
    ''' </summary>
    Public Function CreateBlurredImage(source As Image, Optional factor As Integer = DefaultFactor) As Bitmap
        If source Is Nothing Then Return Nothing
        Dim f = Math.Max(2, Math.Min(48, factor))
        Dim tw = Math.Max(1, source.Width \ f)
        Dim th = Math.Max(1, source.Height \ f)

        Dim result As New Bitmap(source.Width, source.Height, PixelFormat.Format32bppArgb)
        Using tiny As New Bitmap(tw, th, PixelFormat.Format32bppArgb)
            Using g = Graphics.FromImage(tiny)
                g.InterpolationMode = InterpolationMode.HighQualityBilinear
                g.PixelOffsetMode = PixelOffsetMode.HighQuality
                g.DrawImage(source, 0, 0, tw, th)
            End Using
            Using g = Graphics.FromImage(result)
                g.InterpolationMode = InterpolationMode.HighQualityBilinear
                g.PixelOffsetMode = PixelOffsetMode.HighQuality
                g.DrawImage(tiny, 0, 0, result.Width, result.Height)
            End Using
        End Using
        Return result
    End Function
End Module
