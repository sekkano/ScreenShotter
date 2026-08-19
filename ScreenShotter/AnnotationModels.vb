Imports System.Drawing.Drawing2D

''' <summary>
''' Base for post-editable annotations stored in normalized image coordinates (0–1).
''' </summary>
Public MustInherit Class AnnotationBase
    Protected Sub New(Optional id As Guid = Nothing)
        Me.Id = If(id = Guid.Empty, Guid.NewGuid(), id)
    End Sub

    Public ReadOnly Property Id As Guid
    Public Property Color As Color
    ''' <summary>Stroke width or font size in natural image pixels.</summary>
    Public Property NativeSize As Single

    Public MustOverride Function Clone() As AnnotationBase
    Public MustOverride Function GetBounds() As RectangleF
    Public MustOverride Function HitTest(norm As PointF, hitSlop As Single) As Boolean
    Public MustOverride Sub Draw(
        g As Graphics,
        destFrame As Rectangle,
        panX As Single,
        panY As Single,
        contentW As Single,
        contentH As Single,
        strokeScale As Single,
        Optional selected As Boolean = False)
    Public MustOverride Sub Translate(dx As Single, dy As Single)
End Class

''' <summary>Border-only rectangle in normalized image space.</summary>
Public Class RectAnnotation
    Inherits AnnotationBase

    Public Sub New(Optional id As Guid = Nothing)
        MyBase.New(id)
    End Sub

    ''' <summary>Normalized rectangle (may have negative width/height before Normalize).</summary>
    Public Property Bounds As RectangleF

    Public Sub Normalize()
        Bounds = AnnotationHelper.NormalizeRect(Bounds)
    End Sub

    Public Overrides Function Clone() As AnnotationBase
        Return New RectAnnotation(Id) With {
            .Color = Color,
            .NativeSize = NativeSize,
            .Bounds = Bounds
        }
    End Function

    Public Overrides Function GetBounds() As RectangleF
        Return AnnotationHelper.NormalizeRect(Bounds)
    End Function

    Public Overrides Function HitTest(norm As PointF, hitSlop As Single) As Boolean
        Dim r = GetBounds()
        r.Inflate(hitSlop, hitSlop)
        If Not r.Contains(norm) Then Return False
        ' Prefer border hit: inside inflated outer but outside shrunk inner (or small rect → whole area)
        Dim inner = GetBounds()
        Dim shrink = Math.Max(hitSlop * 0.5F, 0.004F)
        If inner.Width > shrink * 3 AndAlso inner.Height > shrink * 3 Then
            inner.Inflate(-shrink, -shrink)
            If inner.Contains(norm) Then Return False
        End If
        Return True
    End Function

    Public Overrides Sub Translate(dx As Single, dy As Single)
        Bounds = New RectangleF(Bounds.X + dx, Bounds.Y + dy, Bounds.Width, Bounds.Height)
    End Sub

    Public Overrides Sub Draw(
        g As Graphics,
        destFrame As Rectangle,
        panX As Single,
        panY As Single,
        contentW As Single,
        contentH As Single,
        strokeScale As Single,
        Optional selected As Boolean = False)
        Dim r = GetBounds()
        Dim tl = AnnotationHelper.NormToDest(New PointF(r.Left, r.Top), destFrame, panX, panY, contentW, contentH)
        Dim br = AnnotationHelper.NormToDest(New PointF(r.Right, r.Bottom), destFrame, panX, panY, contentW, contentH)
        Dim px As New RectangleF(
            Math.Min(tl.X, br.X),
            Math.Min(tl.Y, br.Y),
            Math.Abs(br.X - tl.X),
            Math.Abs(br.Y - tl.Y))
        Dim w = Math.Max(1.0F, NativeSize * strokeScale)
        Using pen As New Pen(Color, w)
            pen.Alignment = PenAlignment.Center
            g.DrawRectangle(pen, px.X, px.Y, Math.Max(1.0F, px.Width), Math.Max(1.0F, px.Height))
        End Using
        If selected Then
            AnnotationHelper.DrawSelectionRect(g, px)
        End If
    End Sub
End Class

''' <summary>Arrow from Start → End in normalized image space.</summary>
Public Class ArrowAnnotation
    Inherits AnnotationBase

    Public Sub New(Optional id As Guid = Nothing)
        MyBase.New(id)
    End Sub

    Public Property Start As PointF
    Public Property [End] As PointF

    Public Overrides Function Clone() As AnnotationBase
        Return New ArrowAnnotation(Id) With {
            .Color = Color,
            .NativeSize = NativeSize,
            .Start = Start,
            .End = [End]
        }
    End Function

    Public Overrides Function GetBounds() As RectangleF
        Dim x1 = Math.Min(Start.X, [End].X)
        Dim y1 = Math.Min(Start.Y, [End].Y)
        Dim x2 = Math.Max(Start.X, [End].X)
        Dim y2 = Math.Max(Start.Y, [End].Y)
        Return RectangleF.FromLTRB(x1, y1, x2, y2)
    End Function

    Public Overrides Function HitTest(norm As PointF, hitSlop As Single) As Boolean
        Return AnnotationHelper.DistanceToSegment(norm, Start, [End]) <= hitSlop OrElse
            AnnotationHelper.Distance(norm, Start) <= hitSlop OrElse
            AnnotationHelper.Distance(norm, [End]) <= hitSlop
    End Function

    Public Overrides Sub Translate(dx As Single, dy As Single)
        Start = New PointF(Start.X + dx, Start.Y + dy)
        [End] = New PointF([End].X + dx, [End].Y + dy)
    End Sub

    Public Overrides Sub Draw(
        g As Graphics,
        destFrame As Rectangle,
        panX As Single,
        panY As Single,
        contentW As Single,
        contentH As Single,
        strokeScale As Single,
        Optional selected As Boolean = False)
        Dim a = AnnotationHelper.NormToDest(Start, destFrame, panX, panY, contentW, contentH)
        Dim b = AnnotationHelper.NormToDest([End], destFrame, panX, panY, contentW, contentH)
        Dim w = Math.Max(1.0F, NativeSize * strokeScale)
        Dim head = AnnotationHelper.ArrowHeadPoints(a, b, w)

        Using pen As New Pen(Color, w)
            pen.StartCap = LineCap.Round
            pen.EndCap = LineCap.Round
            g.DrawLine(pen, a, b)
        End Using
        Using brush As New SolidBrush(Color)
            g.FillPolygon(brush, head)
        End Using
        If selected Then
            AnnotationHelper.DrawEndpointHandles(g, a, b)
        End If
    End Sub
End Class

''' <summary>Axis-aligned text at a normalized top-left location.</summary>
Public Class TextAnnotation
    Inherits AnnotationBase

    Public Sub New(Optional id As Guid = Nothing)
        MyBase.New(id)
    End Sub

    Public Property Location As PointF
    Public Property Text As String = ""

    Public Overrides Function Clone() As AnnotationBase
        Return New TextAnnotation(Id) With {
            .Color = Color,
            .NativeSize = NativeSize,
            .Location = Location,
            .Text = Text
        }
    End Function

    Public Overrides Function GetBounds() As RectangleF
        ' Approximate using font metrics at unit scale; hit-test uses measured size in Draw path via helper
        Dim w = Math.Max(0.02F, Text.Length * NativeSize * 0.00055F)
        Dim h = Math.Max(0.02F, NativeSize * 0.0012F)
        Return New RectangleF(Location.X, Location.Y, w, h)
    End Function

    Public Function MeasureNormalized(naturalSize As Size) As RectangleF
        If naturalSize.Width <= 0 OrElse naturalSize.Height <= 0 Then Return GetBounds()
        Using font As New Font("Segoe UI", Math.Max(6.0F, NativeSize), FontStyle.Regular, GraphicsUnit.Pixel)
            Dim sz = TextRenderer.MeasureText(If(Text, ""), font)
            Return New RectangleF(
                Location.X,
                Location.Y,
                Math.Max(0.01F, sz.Width / CSng(naturalSize.Width)),
                Math.Max(0.01F, sz.Height / CSng(naturalSize.Height)))
        End Using
    End Function

    Public Overrides Function HitTest(norm As PointF, hitSlop As Single) As Boolean
        Dim r = GetBounds()
        r.Inflate(hitSlop, hitSlop)
        Return r.Contains(norm)
    End Function

    Public Function HitTestWithSize(norm As PointF, hitSlop As Single, naturalSize As Size) As Boolean
        Dim r = MeasureNormalized(naturalSize)
        r.Inflate(hitSlop, hitSlop)
        Return r.Contains(norm)
    End Function

    Public Overrides Sub Translate(dx As Single, dy As Single)
        Location = New PointF(Location.X + dx, Location.Y + dy)
    End Sub

    Public Overrides Sub Draw(
        g As Graphics,
        destFrame As Rectangle,
        panX As Single,
        panY As Single,
        contentW As Single,
        contentH As Single,
        strokeScale As Single,
        Optional selected As Boolean = False)
        Dim origin = AnnotationHelper.NormToDest(Location, destFrame, panX, panY, contentW, contentH)
        Dim fontPx = Math.Max(6.0F, NativeSize * strokeScale)
        Using font As New Font("Segoe UI", fontPx, FontStyle.Regular, GraphicsUnit.Pixel)
            Using brush As New SolidBrush(Color)
                g.DrawString(If(Text, ""), font, brush, origin)
            End Using
            If selected Then
                Dim sz = g.MeasureString(If(Text, ""), font)
                AnnotationHelper.DrawSelectionRect(g, New RectangleF(origin.X, origin.Y, sz.Width, sz.Height))
            End If
        End Using
    End Sub
End Class

''' <summary>Pure geometry / drawing helpers for annotations.</summary>
Public Module AnnotationHelper
    Public Const MinFontSize As Single = 8.0F
    Public Const MaxFontSize As Single = 128.0F
    Public Const MinShapeSizeNorm As Single = 0.008F
    Public Const MinArrowLengthNorm As Single = 0.012F

    Public Function ClampFontSize(value As Single) As Single
        If Single.IsNaN(value) OrElse Single.IsInfinity(value) Then Return 24.0F
        Return Math.Max(MinFontSize, Math.Min(MaxFontSize, value))
    End Function

    Public Function NormalizeRect(r As RectangleF) As RectangleF
        Dim x1 = Math.Min(r.Left, r.Right)
        Dim y1 = Math.Min(r.Top, r.Bottom)
        Dim x2 = Math.Max(r.Left, r.Right)
        Dim y2 = Math.Max(r.Top, r.Bottom)
        Return RectangleF.FromLTRB(x1, y1, x2, y2)
    End Function

    Public Function ClampNormalized(p As PointF) As PointF
        Return New PointF(
            Math.Max(0.0F, Math.Min(1.0F, p.X)),
            Math.Max(0.0F, Math.Min(1.0F, p.Y)))
    End Function

    Public Function Distance(a As PointF, b As PointF) As Single
        Dim dx = a.X - b.X
        Dim dy = a.Y - b.Y
        Return CSng(Math.Sqrt(dx * dx + dy * dy))
    End Function

    Public Function DistanceToSegment(p As PointF, a As PointF, b As PointF) As Single
        Dim dx = b.X - a.X
        Dim dy = b.Y - a.Y
        Dim lenSq = dx * dx + dy * dy
        If lenSq < 0.0000001F Then Return Distance(p, a)
        Dim t = ((p.X - a.X) * dx + (p.Y - a.Y) * dy) / lenSq
        t = Math.Max(0.0F, Math.Min(1.0F, t))
        Return Distance(p, New PointF(a.X + t * dx, a.Y + t * dy))
    End Function

    Public Function NormToDest(
        norm As PointF,
        destFrame As Rectangle,
        panX As Single,
        panY As Single,
        contentW As Single,
        contentH As Single) As PointF

        Return New PointF(
            destFrame.X + panX + norm.X * contentW,
            destFrame.Y + panY + norm.Y * contentH)
    End Function

    Public Function ArrowHeadPoints(startPt As PointF, endPt As PointF, strokeWidth As Single) As PointF()
        Dim dx = endPt.X - startPt.X
        Dim dy = endPt.Y - startPt.Y
        Dim len = CSng(Math.Sqrt(dx * dx + dy * dy))
        If len < 1.0F Then
            Return {endPt, endPt, endPt}
        End If
        Dim ux = dx / len
        Dim uy = dy / len
        Dim headLen = Math.Max(strokeWidth * 3.2F, 10.0F)
        Dim headWidth = Math.Max(strokeWidth * 2.2F, 7.0F)
        Dim baseX = endPt.X - ux * headLen
        Dim baseY = endPt.Y - uy * headLen
        Dim px = -uy
        Dim py = ux
        Return {
            endPt,
            New PointF(baseX + px * headWidth, baseY + py * headWidth),
            New PointF(baseX - px * headWidth, baseY - py * headWidth)
        }
    End Function

    Public Sub DrawSelectionRect(g As Graphics, bounds As RectangleF)
        Using pen As New Pen(Color.FromArgb(180, 30, 120, 220), 1.0F)
            pen.DashStyle = DashStyle.Dash
            g.DrawRectangle(pen, bounds.X, bounds.Y, bounds.Width, bounds.Height)
        End Using
    End Sub

    Public Sub DrawEndpointHandles(g As Graphics, a As PointF, b As PointF)
        Const r As Single = 4.0F
        Using brush As New SolidBrush(Color.FromArgb(220, 30, 120, 220))
            g.FillEllipse(brush, a.X - r, a.Y - r, r * 2, r * 2)
            g.FillEllipse(brush, b.X - r, b.Y - r, r * 2, r * 2)
        End Using
    End Sub

    Public Function RectFromCorners(a As PointF, b As PointF) As RectangleF
        Return NormalizeRect(RectangleF.FromLTRB(a.X, a.Y, b.X, b.Y))
    End Function
End Module
