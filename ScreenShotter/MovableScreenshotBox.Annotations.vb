''' <summary>
''' Shape / arrow / text annotation create + edit for MovableScreenshotBox.
''' </summary>
Partial Public Class MovableScreenshotBox

    Private Sub ClearDrawingSelection()
        If _selectedAnnotation Is Nothing AndAlso _selectedStroke Is Nothing Then Return
        _selectedAnnotation = Nothing
        _selectedStroke = Nothing
        Invalidate()
    End Sub

    Private Sub ClearAnnotationSelection()
        ClearDrawingSelection()
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
        SelectAnnotation(draft)
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
        _canvas?.RecordAnnotationAdded(ItemId, ann)
        SelectAnnotation(ann)
    End Sub

    Private Sub SelectAnnotation(ann As AnnotationBase)
        _selectedStroke = Nothing
        _selectedAnnotation = ann
        Invalidate()
        _canvas?.NotifyAnnotationSelected(ann)
    End Sub

    Private Function TryBeginAnnotationEdit(local As Point) As Boolean
        If _canvas Is Nothing OrElse _canvas.ActiveTool <> DrawingTool.Pointer Then
            Return False
        End If

        Dim norm = DrawingHelper.ViewportToNormalized(local, Size, _pan, _zoom)
        If Not norm.HasValue Then
            ClearDrawingSelection()
            Return False
        End If
        Dim p = DrawingHelper.ClampNormalized(norm.Value)
        Dim hitSlop = 0.025F
        Dim cornerSlop = 0.03F

        ' Shapes sit above freehand ink in paint order — hit-test them first
        For i = _annotations.Count - 1 To 0 Step -1
            Dim ann = _annotations(i)
            Dim kind = AnnotEditKind.None
            Dim handle = ""

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
                Else
                    Dim rect = TryCast(ann, RectAnnotation)
                    If rect IsNot Nothing AndAlso rect.HitTest(p, hitSlop) Then
                        handle = AnnotationHelper.HitTestRectHandle(rect.GetBounds(), p, cornerSlop, hitSlop)
                        If handle = "MOVE" OrElse handle = "" Then
                            kind = AnnotEditKind.Move
                            handle = "MOVE"
                        Else
                            kind = AnnotEditKind.ResizeRect
                        End If
                    End If
                End If
            End If

            If kind = AnnotEditKind.None Then Continue For

            _selectedStroke = Nothing
            SelectAnnotation(ann)
            _annotEditOriginal = ann.Clone()
            _annotEditKind = kind
            _annotEditGrabNorm = p
            _annotRectHandle = handle
            _mode = InteractMode.AnnotEdit
            Capture = True
            Cursor = CursorForAnnotationEdit(kind, handle)
            Return True
        Next

        ' Freehand pen / highlighter / blur — select and drag to move
        For i = _strokes.Count - 1 To 0 Step -1
            Dim stroke = _strokes(i)
            Dim strokeSlop = HitSlopForStroke(stroke)
            If DrawingHelper.HitTestStroke(stroke, p, strokeSlop) Then
                SelectStroke(stroke)
                _strokeEditOriginalPoints = ClonePointList(stroke.Points)
                _annotEditKind = AnnotEditKind.MoveStroke
                _annotEditGrabNorm = p
                _annotEditOriginal = Nothing
                _mode = InteractMode.AnnotEdit
                Capture = True
                Cursor = Cursors.SizeAll
                Return True
            End If
        Next

        ClearDrawingSelection()
        Return False
    End Function

    Private Function HitSlopForStroke(stroke As InkStroke) As Single
        If _naturalSize.Width <= 0 Then Return 0.02F
        Dim half = stroke.NativeWidth / CSng(_naturalSize.Width) * 0.55F
        Return Math.Max(0.012F, half + 0.008F)
    End Function

    Private Sub SelectStroke(stroke As InkStroke)
        _selectedAnnotation = Nothing
        _selectedStroke = stroke
        Invalidate()
        _canvas?.NotifyStrokeSelected(stroke)
    End Sub

    Private Shared Function ClonePointList(points As IList(Of PointF)) As List(Of PointF)
        Return New List(Of PointF)(points)
    End Function

    Private Shared Sub ApplyStrokePoints(stroke As InkStroke, points As IList(Of PointF))
        If stroke Is Nothing OrElse points Is Nothing Then Return
        stroke.Points.Clear()
        stroke.Points.AddRange(points)
    End Sub

    Private Shared Sub TranslateStrokePoints(stroke As InkStroke, source As IList(Of PointF), dx As Single, dy As Single)
        If stroke Is Nothing OrElse source Is Nothing Then Return
        stroke.Points.Clear()
        For Each pt In source
            stroke.Points.Add(AnnotationHelper.ClampNormalized(New PointF(pt.X + dx, pt.Y + dy)))
        Next
    End Sub

    Private Shared Function CursorForAnnotationEdit(kind As AnnotEditKind, handle As String) As Cursor
        If kind = AnnotEditKind.ResizeRect Then
            Return AnnotationHelper.CursorForRectHandle(handle)
        End If
        Return Cursors.SizeAll
    End Function

    Private Sub UpdateAnnotationEdit(local As Point)
        Dim norm = DrawingHelper.ViewportToNormalized(local, Size, _pan, _zoom)
        Dim p = If(norm.HasValue, DrawingHelper.ClampNormalized(norm.Value), _annotEditGrabNorm)
        Dim dx = p.X - _annotEditGrabNorm.X
        Dim dy = p.Y - _annotEditGrabNorm.Y

        If _annotEditKind = AnnotEditKind.MoveStroke Then
            If _selectedStroke IsNot Nothing AndAlso _strokeEditOriginalPoints IsNot Nothing Then
                TranslateStrokePoints(_selectedStroke, _strokeEditOriginalPoints, dx, dy)
                Cursor = Cursors.SizeAll
                Invalidate()
            End If
            Return
        End If

        If _selectedAnnotation Is Nothing OrElse _annotEditOriginal Is Nothing Then Return

        Select Case _annotEditKind
            Case AnnotEditKind.Move
                CopyAnnotationState(_annotEditOriginal, _selectedAnnotation)
                _selectedAnnotation.Translate(dx, dy)
                Cursor = Cursors.SizeAll

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
                    rect.Bounds = AnnotationHelper.ResizeRectFromHandle(orig.GetBounds(), _annotRectHandle, p)
                    Cursor = AnnotationHelper.CursorForRectHandle(_annotRectHandle)
                End If
        End Select
        Invalidate()
    End Sub

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
        If _annotEditKind = AnnotEditKind.MoveStroke Then
            CommitStrokeMove()
            Return
        End If

        If _selectedAnnotation Is Nothing OrElse _annotEditOriginal Is Nothing Then
            _annotEditKind = AnnotEditKind.None
            _annotEditOriginal = Nothing
            _annotRectHandle = ""
            Return
        End If
        Dim before = _annotEditOriginal
        Dim after = _selectedAnnotation.Clone()
        _annotEditKind = AnnotEditKind.None
        _annotEditOriginal = Nothing
        _annotRectHandle = ""
        If Not AnnotationStatesEqual(before, after) Then
            _canvas?.RecordAnnotationChanged(ItemId, before, after)
        End If
        Invalidate()
    End Sub

    Private Sub CommitStrokeMove()
        Dim stroke = _selectedStroke
        Dim before = _strokeEditOriginalPoints
        _annotEditKind = AnnotEditKind.None
        _strokeEditOriginalPoints = Nothing
        If stroke Is Nothing OrElse before Is Nothing Then
            Invalidate()
            Return
        End If
        Dim after = ClonePointList(stroke.Points)
        If Not PointListsEqual(before, after) Then
            _canvas?.RecordStrokeMoved(ItemId, stroke, before, after)
        End If
        Invalidate()
    End Sub

    Private Shared Function PointListsEqual(a As IList(Of PointF), b As IList(Of PointF)) As Boolean
        If a Is Nothing OrElse b Is Nothing Then Return a Is b
        If a.Count <> b.Count Then Return False
        For i = 0 To a.Count - 1
            If Math.Abs(a(i).X - b(i).X) > 0.0001F OrElse Math.Abs(a(i).Y - b(i).Y) > 0.0001F Then
                Return False
            End If
        Next
        Return True
    End Function

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

    ''' <summary>Pointer hover cursor over a selected/hovered annotation.</summary>
    Private Function CursorForAnnotationHover(local As Point) As Cursor
        If _canvas Is Nothing OrElse _canvas.ActiveTool <> DrawingTool.Pointer Then
            Return Nothing
        End If
        Dim norm = DrawingHelper.ViewportToNormalized(local, Size, _pan, _zoom)
        If Not norm.HasValue Then Return Nothing
        Dim p = DrawingHelper.ClampNormalized(norm.Value)
        For i = _annotations.Count - 1 To 0 Step -1
            Dim rect = TryCast(_annotations(i), RectAnnotation)
            If rect IsNot Nothing AndAlso rect.HitTest(p, 0.025F) Then
                Dim handle = AnnotationHelper.HitTestRectHandle(rect.GetBounds(), p, 0.03F, 0.025F)
                Return AnnotationHelper.CursorForRectHandle(If(String.IsNullOrEmpty(handle), "MOVE", handle))
            End If
            If _annotations(i).HitTest(p, 0.025F) Then
                Return Cursors.SizeAll
            End If
            Dim text = TryCast(_annotations(i), TextAnnotation)
            If text IsNot Nothing AndAlso text.HitTestWithSize(p, 0.025F, _naturalSize) Then
                Return Cursors.SizeAll
            End If
        Next
        Return Nothing
    End Function
End Class
