''' <summary>
''' Shape / arrow / text annotation create + edit for MovableScreenshotBox.
''' </summary>
Partial Public Class MovableScreenshotBox

    Private Sub ClearAnnotationSelection()
        If _selectedAnnotation Is Nothing Then Return
        _selectedAnnotation = Nothing
        Invalidate()
    End Sub

    Private Sub BeginAnnotate(local As Point, tool As DrawingTool)
        Dim norm = DrawingHelper.ViewportToNormalized(local, Size, _pan, _zoom)
        If Not norm.HasValue Then Return
        Dim p = DrawingHelper.ClampNormalized(norm.Value)
        _annotateTool = tool
        _annotateStartNorm = p
        _annotationDraft = Nothing
        _pendingTextClick = (tool = DrawingTool.Text)
        _pendingTextLocal = local

        If tool = DrawingTool.Text Then
            ' Wait for mouse-up without drag to place text
            _mode = InteractMode.None
            Capture = False
            Return
        End If

        _mode = InteractMode.Annotate
        Capture = True
        Cursor = Cursors.Cross
        UpdateAnnotateDraft(local)
    End Sub

    Private Sub UpdateAnnotateDraft(local As Point)
        Dim norm = DrawingHelper.ViewportToNormalized(local, Size, _pan, _zoom)
        Dim p = If(norm.HasValue, DrawingHelper.ClampNormalized(norm.Value), _annotateStartNorm)

        Select Case _annotateTool
            Case DrawingTool.Rectangle
                _annotationDraft = _canvas.DrawingSettings.CreateRectAnnotation(
                    AnnotationHelper.RectFromCorners(_annotateStartNorm, p))
            Case DrawingTool.Arrow
                _annotationDraft = _canvas.DrawingSettings.CreateArrowAnnotation(_annotateStartNorm, p)
            Case Else
                _annotationDraft = Nothing
        End Select
        Invalidate()
    End Sub

    Private Function CommitAnnotateDraft(local As Point) As AnnotationBase
        UpdateAnnotateDraft(local)
        Dim draft = _annotationDraft
        _annotationDraft = Nothing
        If draft Is Nothing Then Return Nothing

        Dim rect = TryCast(draft, RectAnnotation)
        If rect IsNot Nothing Then
            Dim b = rect.GetBounds()
            If b.Width < AnnotationHelper.MinShapeSizeNorm OrElse b.Height < AnnotationHelper.MinShapeSizeNorm Then
                Invalidate()
                Return Nothing
            End If
        End If

        Dim arrow = TryCast(draft, ArrowAnnotation)
        If arrow IsNot Nothing Then
            If AnnotationHelper.Distance(arrow.Start, arrow.End) < AnnotationHelper.MinArrowLengthNorm Then
                Invalidate()
                Return Nothing
            End If
        End If

        _annotations.Add(draft)
        _selectedAnnotation = draft
        Invalidate()
        Return draft
    End Function

    Private Sub PlaceTextAt(local As Point)
        Dim norm = DrawingHelper.ViewportToNormalized(local, Size, _pan, _zoom)
        If Not norm.HasValue Then Return
        Dim p = DrawingHelper.ClampNormalized(norm.Value)

        Dim proposed = Interaction.InputBox(
            "Enter text for this annotation:",
            "Text",
            "Text")
        If String.IsNullOrWhiteSpace(proposed) Then Return

        Dim ann = _canvas.DrawingSettings.CreateTextAnnotation(p, proposed.Trim())
        _annotations.Add(ann)
        _selectedAnnotation = ann
        _canvas?.RecordAnnotationAdded(ItemId, ann)
        Invalidate()
    End Sub

    Private Function TryBeginAnnotationEdit(local As Point) As Boolean
        If _canvas Is Nothing OrElse _canvas.ActiveTool <> DrawingTool.Pointer Then
            Return False
        End If

        Dim norm = DrawingHelper.ViewportToNormalized(local, Size, _pan, _zoom)
        If Not norm.HasValue Then
            ClearAnnotationSelection()
            Return False
        End If
        Dim p = DrawingHelper.ClampNormalized(norm.Value)
        Dim hitSlop = 0.02F

        ' Top-most first
        For i = _annotations.Count - 1 To 0 Step -1
            Dim ann = _annotations(i)
            Dim kind = AnnotEditKind.None

            Dim arrow = TryCast(ann, ArrowAnnotation)
            If arrow IsNot Nothing Then
                If AnnotationHelper.Distance(p, arrow.Start) <= hitSlop Then
                    kind = AnnotEditKind.MoveArrowStart
                ElseIf AnnotationHelper.Distance(p, arrow.End) <= hitSlop Then
                    kind = AnnotEditKind.MoveArrowEnd
                ElseIf arrow.HitTest(p, hitSlop) Then
                    kind = AnnotEditKind.Move
                End If
            Else
                Dim text = TryCast(ann, TextAnnotation)
                If text IsNot Nothing Then
                    If text.HitTestWithSize(p, hitSlop, _naturalSize) Then
                        kind = AnnotEditKind.Move
                    End If
                ElseIf ann.HitTest(p, hitSlop) Then
                    Dim rect = TryCast(ann, RectAnnotation)
                    If rect IsNot Nothing Then
                        kind = AnnotEditKind.ResizeRect
                        ' Interior of small rects: move instead
                        Dim b = rect.GetBounds()
                        Dim inner = b
                        inner.Inflate(-0.012F, -0.012F)
                        If inner.Width > 0.01F AndAlso inner.Height > 0.01F AndAlso inner.Contains(p) Then
                            kind = AnnotEditKind.Move
                        ElseIf Not NearRectBorder(b, p, hitSlop) Then
                            kind = AnnotEditKind.Move
                        End If
                    Else
                        kind = AnnotEditKind.Move
                    End If
                End If
            End If

            If kind = AnnotEditKind.None Then Continue For

            _selectedAnnotation = ann
            _annotEditOriginal = ann.Clone()
            _annotEditKind = kind
            _annotEditGrabNorm = p
            _mode = InteractMode.AnnotEdit
            Capture = True
            Cursor = Cursors.SizeAll
            Invalidate()
            Return True
        Next

        ClearAnnotationSelection()
        Return False
    End Function

    Private Shared Function NearRectBorder(b As RectangleF, p As PointF, slop As Single) As Boolean
        Dim outer = b
        outer.Inflate(slop, slop)
        If Not outer.Contains(p) Then Return False
        Dim inner = b
        inner.Inflate(-slop, -slop)
        If inner.Width <= 0 OrElse inner.Height <= 0 Then Return True
        Return Not inner.Contains(p)
    End Function

    Private Sub UpdateAnnotationEdit(local As Point)
        If _selectedAnnotation Is Nothing OrElse _annotEditOriginal Is Nothing Then Return
        Dim norm = DrawingHelper.ViewportToNormalized(local, Size, _pan, _zoom)
        Dim p = If(norm.HasValue, DrawingHelper.ClampNormalized(norm.Value), _annotEditGrabNorm)
        Dim dx = p.X - _annotEditGrabNorm.X
        Dim dy = p.Y - _annotEditGrabNorm.Y

        Select Case _annotEditKind
            Case AnnotEditKind.Move
                ' Reset from original then translate (avoids drift)
                CopyAnnotationState(_annotEditOriginal, _selectedAnnotation)
                _selectedAnnotation.Translate(dx, dy)

            Case AnnotEditKind.MoveArrowStart
                Dim arrow = TryCast(_selectedAnnotation, ArrowAnnotation)
                If arrow IsNot Nothing Then arrow.Start = p

            Case AnnotEditKind.MoveArrowEnd
                Dim arrow = TryCast(_selectedAnnotation, ArrowAnnotation)
                If arrow IsNot Nothing Then arrow.End = p

            Case AnnotEditKind.ResizeRect
                Dim rect = TryCast(_selectedAnnotation, RectAnnotation)
                Dim orig = TryCast(_annotEditOriginal, RectAnnotation)
                If rect IsNot Nothing AndAlso orig IsNot Nothing Then
                    ' Anchor opposite corner from grab relative to original bounds
                    Dim b = orig.GetBounds()
                    Dim anchor As PointF
                    If Math.Abs(_annotEditGrabNorm.X - b.Left) <= Math.Abs(_annotEditGrabNorm.X - b.Right) Then
                        anchor = New PointF(b.Right, If(Math.Abs(_annotEditGrabNorm.Y - b.Top) <= Math.Abs(_annotEditGrabNorm.Y - b.Bottom), b.Bottom, b.Top))
                        If Math.Abs(_annotEditGrabNorm.Y - b.Top) > Math.Abs(_annotEditGrabNorm.Y - b.Bottom) Then
                            anchor = New PointF(b.Right, b.Top)
                        Else
                            anchor = New PointF(b.Right, b.Bottom)
                        End If
                    Else
                        If Math.Abs(_annotEditGrabNorm.Y - b.Top) <= Math.Abs(_annotEditGrabNorm.Y - b.Bottom) Then
                            anchor = New PointF(b.Left, b.Bottom)
                        Else
                            anchor = New PointF(b.Left, b.Top)
                        End If
                    End If
                    ' Simpler: resize from fixed opposite corner based on which corner grab is nearest
                    anchor = OppositeCorner(b, NearestCorner(b, _annotEditGrabNorm))
                    rect.Bounds = AnnotationHelper.RectFromCorners(anchor, p)
                End If
        End Select
        Invalidate()
    End Sub

    Private Shared Function NearestCorner(b As RectangleF, p As PointF) As PointF
        Dim corners = {
            New PointF(b.Left, b.Top),
            New PointF(b.Right, b.Top),
            New PointF(b.Left, b.Bottom),
            New PointF(b.Right, b.Bottom)
        }
        Dim best = corners(0)
        Dim bestD = AnnotationHelper.Distance(p, best)
        For i = 1 To corners.Length - 1
            Dim d = AnnotationHelper.Distance(p, corners(i))
            If d < bestD Then
                bestD = d
                best = corners(i)
            End If
        Next
        Return best
    End Function

    Private Shared Function OppositeCorner(b As RectangleF, corner As PointF) As PointF
        Dim cx = If(Math.Abs(corner.X - b.Left) < Math.Abs(corner.X - b.Right), b.Right, b.Left)
        Dim cy = If(Math.Abs(corner.Y - b.Top) < Math.Abs(corner.Y - b.Bottom), b.Bottom, b.Top)
        Return New PointF(cx, cy)
    End Function

    Private Shared Sub CopyAnnotationState(source As AnnotationBase, dest As AnnotationBase)
        dest.Color = source.Color
        dest.NativeSize = source.NativeSize
        Dim rs = TryCast(source, RectAnnotation)
        Dim rd = TryCast(dest, RectAnnotation)
        If rs IsNot Nothing AndAlso rd IsNot Nothing Then
            rd.Bounds = rs.Bounds
            Return
        End If
        Dim asrc = TryCast(source, ArrowAnnotation)
        Dim ad = TryCast(dest, ArrowAnnotation)
        If asrc IsNot Nothing AndAlso ad IsNot Nothing Then
            ad.Start = asrc.Start
            ad.End = asrc.End
            Return
        End If
        Dim ts = TryCast(source, TextAnnotation)
        Dim td = TryCast(dest, TextAnnotation)
        If ts IsNot Nothing AndAlso td IsNot Nothing Then
            td.Location = ts.Location
            td.Text = ts.Text
        End If
    End Sub

    Private Sub CommitAnnotationEdit()
        If _selectedAnnotation Is Nothing OrElse _annotEditOriginal Is Nothing Then
            _annotEditKind = AnnotEditKind.None
            _annotEditOriginal = Nothing
            Return
        End If
        Dim before = _annotEditOriginal
        Dim after = _selectedAnnotation.Clone()
        _annotEditKind = AnnotEditKind.None
        _annotEditOriginal = Nothing
        If Not AnnotationStatesEqual(before, after) Then
            _canvas?.RecordAnnotationChanged(ItemId, before, after)
        End If
        Invalidate()
    End Sub

    Private Shared Function AnnotationStatesEqual(a As AnnotationBase, b As AnnotationBase) As Boolean
        If a Is Nothing OrElse b Is Nothing Then Return a Is b
        If a.GetType() IsNot b.GetType() Then Return False
        If a.Color <> b.Color OrElse Math.Abs(a.NativeSize - b.NativeSize) > 0.01F Then Return False
        Dim ra = TryCast(a, RectAnnotation)
        Dim rb = TryCast(b, RectAnnotation)
        If ra IsNot Nothing AndAlso rb IsNot Nothing Then
            Dim aa = ra.GetBounds()
            Dim bb = rb.GetBounds()
            Return Math.Abs(aa.X - bb.X) < 0.0001F AndAlso Math.Abs(aa.Y - bb.Y) < 0.0001F AndAlso
                Math.Abs(aa.Width - bb.Width) < 0.0001F AndAlso Math.Abs(aa.Height - bb.Height) < 0.0001F
        End If
        Dim aaA = TryCast(a, ArrowAnnotation)
        Dim aaB = TryCast(b, ArrowAnnotation)
        If aaA IsNot Nothing AndAlso aaB IsNot Nothing Then
            Return AnnotationHelper.Distance(aaA.Start, aaB.Start) < 0.0001F AndAlso
                AnnotationHelper.Distance(aaA.End, aaB.End) < 0.0001F
        End If
        Dim ta = TryCast(a, TextAnnotation)
        Dim tb = TryCast(b, TextAnnotation)
        If ta IsNot Nothing AndAlso tb IsNot Nothing Then
            Return ta.Text = tb.Text AndAlso AnnotationHelper.Distance(ta.Location, tb.Location) < 0.0001F
        End If
        Return False
    End Function

    ''' <summary>
    ''' Applies the live before/after clone from undo onto the matching annotation instance.
    ''' </summary>
    Public Sub ApplyAnnotationState(state As AnnotationBase)
        If state Is Nothing Then Return
        For Each ann In _annotations
            If ann.Id = state.Id Then
                CopyAnnotationState(state, ann)
                Invalidate()
                Return
            End If
        Next
    End Sub

    Private Sub DrawAllAnnotations(
        g As Graphics,
        destFrame As Rectangle,
        scaleX As Double,
        scaleY As Double,
        selectedId As Guid)

        For Each ann In _annotations
            DrawOneAnnotation(g, ann, destFrame, scaleX, scaleY, selected:=(ann.Id = selectedId))
        Next
    End Sub

    Private Sub DrawOneAnnotation(
        g As Graphics,
        ann As AnnotationBase,
        destFrame As Rectangle,
        scaleX As Double,
        scaleY As Double,
        selected As Boolean)

        If ann Is Nothing Then Return
        Dim content = ZoomHelper.ContentSize(Size, _zoom)
        Dim panX = CSng(_pan.X * scaleX)
        Dim panY = CSng(_pan.Y * scaleY)
        Dim contentW = Math.Max(1.0F, CSng(content.Width * scaleX))
        Dim contentH = Math.Max(1.0F, CSng(content.Height * scaleY))
        Dim strokeScale =
            DrawingHelper.ViewportStrokeWidth(1.0F, Size, _naturalSize, _zoom) *
            CSng((scaleX + scaleY) / 2.0)
        ann.Draw(g, destFrame, panX, panY, contentW, contentH, strokeScale, selected)
    End Sub
End Class
