''' <summary>
''' Undo adding an ink stroke to a screenshot.
''' </summary>
Public Class StrokeHistoryAction
    Implements IUndoAction

    Private ReadOnly _canvas As ScreenshotCanvas
    Private ReadOnly _itemId As Guid
    Private ReadOnly _stroke As InkStroke

    Public Sub New(canvas As ScreenshotCanvas, itemId As Guid, stroke As InkStroke)
        _canvas = canvas
        _itemId = itemId
        _stroke = stroke
    End Sub

    Public ReadOnly Property Description As String Implements IUndoAction.Description
        Get
            Return "Draw stroke"
        End Get
    End Property

    Public Sub Undo() Implements IUndoAction.Undo
        Dim box = _canvas.FindBox(_itemId)
        box?.RemoveStroke(_stroke)
    End Sub

    Public Sub Redo() Implements IUndoAction.Redo
        Dim box = _canvas.FindBox(_itemId)
        box?.AddStroke(_stroke)
    End Sub
End Class

''' <summary>
''' Undo deleting a freehand stroke (pen / highlighter / blur).
''' </summary>
Public Class StrokeRemoveHistoryAction
    Implements IUndoAction

    Private ReadOnly _canvas As ScreenshotCanvas
    Private ReadOnly _itemId As Guid
    Private ReadOnly _stroke As InkStroke

    Public Sub New(canvas As ScreenshotCanvas, itemId As Guid, stroke As InkStroke)
        _canvas = canvas
        _itemId = itemId
        _stroke = stroke
    End Sub

    Public ReadOnly Property Description As String Implements IUndoAction.Description
        Get
            Return "Delete stroke"
        End Get
    End Property

    Public Sub Undo() Implements IUndoAction.Undo
        Dim box = _canvas.FindBox(_itemId)
        box?.AddStroke(_stroke)
    End Sub

    Public Sub Redo() Implements IUndoAction.Redo
        Dim box = _canvas.FindBox(_itemId)
        box?.RemoveStroke(_stroke)
    End Sub
End Class

''' <summary>
''' Undo restyling a freehand stroke (color / thickness).
''' </summary>
Public Class StrokeStyleHistoryAction
    Implements IUndoAction

    Private ReadOnly _stroke As InkStroke
    Private ReadOnly _beforeColor As Color
    Private ReadOnly _afterColor As Color
    Private ReadOnly _beforeWidth As Single
    Private ReadOnly _afterWidth As Single
    Private ReadOnly _canvas As ScreenshotCanvas
    Private ReadOnly _itemId As Guid

    Public Sub New(
        canvas As ScreenshotCanvas,
        itemId As Guid,
        stroke As InkStroke,
        beforeColor As Color,
        afterColor As Color,
        beforeWidth As Single,
        afterWidth As Single)

        _canvas = canvas
        _itemId = itemId
        _stroke = stroke
        _beforeColor = beforeColor
        _afterColor = afterColor
        _beforeWidth = beforeWidth
        _afterWidth = afterWidth
    End Sub

    Public ReadOnly Property Description As String Implements IUndoAction.Description
        Get
            Return "Restyle stroke"
        End Get
    End Property

    Public Sub Undo() Implements IUndoAction.Undo
        Apply(_beforeColor, _beforeWidth)
    End Sub

    Public Sub Redo() Implements IUndoAction.Redo
        Apply(_afterColor, _afterWidth)
    End Sub

    Private Sub Apply(color As Color, width As Single)
        If _stroke Is Nothing Then Return
        _stroke.Color = color
        _stroke.NativeWidth = width
        _canvas.FindBox(_itemId)?.Invalidate()
    End Sub
End Class

''' <summary>
''' Undo move / resize / zoom / pan of a screenshot.
''' </summary>
Public Class TransformHistoryAction
    Implements IUndoAction

    Private ReadOnly _canvas As ScreenshotCanvas
    Private ReadOnly _itemId As Guid
    Private ReadOnly _before As BoxTransformState
    Private ReadOnly _after As BoxTransformState

    Public Sub New(canvas As ScreenshotCanvas, itemId As Guid, before As BoxTransformState, after As BoxTransformState)
        _canvas = canvas
        _itemId = itemId
        _before = before
        _after = after
    End Sub

    Public ReadOnly Property Description As String Implements IUndoAction.Description
        Get
            Return "Transform screenshot"
        End Get
    End Property

    Public Sub Undo() Implements IUndoAction.Undo
        Dim box = _canvas.FindBox(_itemId)
        box?.ApplyTransform(_before)
        _canvas.SyncBoxModel(_itemId)
        _canvas.UpdateScrollBounds()
    End Sub

    Public Sub Redo() Implements IUndoAction.Redo
        Dim box = _canvas.FindBox(_itemId)
        box?.ApplyTransform(_after)
        _canvas.SyncBoxModel(_itemId)
        _canvas.UpdateScrollBounds()
    End Sub
End Class

''' <summary>
''' Undo adding a new screenshot (capture).
''' </summary>
Public Class AddScreenshotHistoryAction
    Implements IUndoAction
    Implements IDisposable

    Private ReadOnly _canvas As ScreenshotCanvas
    Private ReadOnly _itemId As Guid
    Private _snapshot As ScreenshotSnapshot
    Private _disposed As Boolean

    Public Sub New(canvas As ScreenshotCanvas, itemId As Guid)
        _canvas = canvas
        _itemId = itemId
    End Sub

    Public ReadOnly Property Description As String Implements IUndoAction.Description
        Get
            Return "Add screenshot"
        End Get
    End Property

    Public Sub Undo() Implements IUndoAction.Undo
        ' Replace any prior snapshot so we don't leak image clones.
        _snapshot?.DisposeImage()
        _snapshot = _canvas.TakeSnapshot(_itemId)
        If _snapshot IsNot Nothing Then
            _canvas.RemoveScreenshot(_itemId, recordHistory:=False)
        End If
    End Sub

    Public Sub Redo() Implements IUndoAction.Redo
        If _snapshot IsNot Nothing Then
            _canvas.RestoreSnapshot(_snapshot)
        End If
    End Sub

    Public Sub Dispose() Implements IDisposable.Dispose
        If _disposed Then Return
        _disposed = True
        _snapshot?.DisposeImage()
        _snapshot = Nothing
    End Sub
End Class

''' <summary>
''' Undo deleting a screenshot.
''' </summary>
Public Class DeleteScreenshotHistoryAction
    Implements IUndoAction
    Implements IDisposable

    Private ReadOnly _canvas As ScreenshotCanvas
    Private ReadOnly _snapshot As ScreenshotSnapshot
    Private _disposed As Boolean

    Public Sub New(canvas As ScreenshotCanvas, snapshot As ScreenshotSnapshot)
        _canvas = canvas
        _snapshot = snapshot
    End Sub

    Public ReadOnly Property Description As String Implements IUndoAction.Description
        Get
            Return "Delete screenshot"
        End Get
    End Property

    Public Sub Undo() Implements IUndoAction.Undo
        _canvas.RestoreSnapshot(_snapshot)
    End Sub

    Public Sub Redo() Implements IUndoAction.Redo
        If _snapshot IsNot Nothing Then
            _canvas.RemoveScreenshot(_snapshot.ItemId, recordHistory:=False)
        End If
    End Sub

    Public Sub Dispose() Implements IDisposable.Dispose
        If _disposed Then Return
        _disposed = True
        _snapshot?.DisposeImage()
    End Sub
End Class

''' <summary>
''' Undo adding a shape / arrow / text annotation.
''' </summary>
Public Class AnnotationAddHistoryAction
    Implements IUndoAction

    Private ReadOnly _canvas As ScreenshotCanvas
    Private ReadOnly _itemId As Guid
    Private ReadOnly _annotation As AnnotationBase

    Public Sub New(canvas As ScreenshotCanvas, itemId As Guid, annotation As AnnotationBase)
        _canvas = canvas
        _itemId = itemId
        _annotation = annotation
    End Sub

    Public ReadOnly Property Description As String Implements IUndoAction.Description
        Get
            Return "Add annotation"
        End Get
    End Property

    Public Sub Undo() Implements IUndoAction.Undo
        Dim box = _canvas.FindBox(_itemId)
        box?.RemoveAnnotation(_annotation)
    End Sub

    Public Sub Redo() Implements IUndoAction.Redo
        Dim box = _canvas.FindBox(_itemId)
        box?.AddAnnotation(_annotation)
    End Sub
End Class

''' <summary>
''' Undo deleting a shape / arrow / text annotation.
''' </summary>
Public Class AnnotationRemoveHistoryAction
    Implements IUndoAction

    Private ReadOnly _canvas As ScreenshotCanvas
    Private ReadOnly _itemId As Guid
    Private ReadOnly _annotation As AnnotationBase

    Public Sub New(canvas As ScreenshotCanvas, itemId As Guid, annotation As AnnotationBase)
        _canvas = canvas
        _itemId = itemId
        _annotation = annotation
    End Sub

    Public ReadOnly Property Description As String Implements IUndoAction.Description
        Get
            Return "Delete annotation"
        End Get
    End Property

    Public Sub Undo() Implements IUndoAction.Undo
        Dim box = _canvas.FindBox(_itemId)
        box?.AddAnnotation(_annotation)
    End Sub

    Public Sub Redo() Implements IUndoAction.Redo
        Dim box = _canvas.FindBox(_itemId)
        box?.RemoveAnnotation(_annotation)
    End Sub
End Class

''' <summary>
''' Undo an edit to an existing annotation (move / resize / restyle).
''' </summary>
Public Class AnnotationEditHistoryAction
    Implements IUndoAction

    Private ReadOnly _canvas As ScreenshotCanvas
    Private ReadOnly _itemId As Guid
    Private ReadOnly _before As AnnotationBase
    Private ReadOnly _after As AnnotationBase

    Public Sub New(canvas As ScreenshotCanvas, itemId As Guid, before As AnnotationBase, after As AnnotationBase)
        _canvas = canvas
        _itemId = itemId
        _before = before
        _after = after
    End Sub

    Public ReadOnly Property Description As String Implements IUndoAction.Description
        Get
            Return "Edit annotation"
        End Get
    End Property

    Public Sub Undo() Implements IUndoAction.Undo
        Dim box = _canvas.FindBox(_itemId)
        box?.ApplyAnnotationState(_before)
    End Sub

    Public Sub Redo() Implements IUndoAction.Redo
        Dim box = _canvas.FindBox(_itemId)
        box?.ApplyAnnotationState(_after)
    End Sub
End Class

''' <summary>
''' Full snapshot used to restore a deleted / re-added screenshot.
''' Owns a clone of the image until the action is discarded with the history stack.
''' </summary>
Public Class ScreenshotSnapshot
    Public Property ItemId As Guid
    Public Property Image As Image
    Public Property Transform As BoxTransformState
    Public Property Strokes As List(Of InkStroke)
    Public Property Annotations As List(Of AnnotationBase)

    Public Sub DisposeImage()
        Image?.Dispose()
        Image = Nothing
    End Sub
End Class

''' <summary>Args for AnnotationSelected on the canvas.</summary>
Public Class AnnotationSelectedEventArgs
    Inherits EventArgs

    Public Sub New(annotation As AnnotationBase)
        Me.Annotation = annotation
    End Sub

    Public ReadOnly Property Annotation As AnnotationBase
End Class

''' <summary>Args for StrokeSelected on the canvas.</summary>
Public Class StrokeSelectedEventArgs
    Inherits EventArgs

    Public Sub New(stroke As InkStroke)
        Me.Stroke = stroke
    End Sub

    Public ReadOnly Property Stroke As InkStroke
End Class
