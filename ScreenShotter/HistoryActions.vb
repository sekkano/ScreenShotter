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
''' Full snapshot used to restore a deleted / re-added screenshot.
''' Owns a clone of the image until the action is discarded with the history stack.
''' </summary>
Public Class ScreenshotSnapshot
    Public Property ItemId As Guid
    Public Property Image As Image
    Public Property Transform As BoxTransformState
    Public Property Strokes As List(Of InkStroke)

    Public Sub DisposeImage()
        Image?.Dispose()
        Image = Nothing
    End Sub
End Class
