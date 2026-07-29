''' <summary>
''' A reversible edit on a tab canvas.
''' </summary>
Public Interface IUndoAction
    ReadOnly Property Description As String
    Sub Undo()
    Sub Redo()
End Interface

''' <summary>
''' Pure undo/redo stack (max depth). Tests can drive this without UI.
''' </summary>
Public Class UndoRedoStack
    Private ReadOnly _undo As New List(Of IUndoAction)()
    Private ReadOnly _redo As New List(Of IUndoAction)()
    Private ReadOnly _maxDepth As Integer

    Public Sub New(Optional maxDepth As Integer = 50)
        _maxDepth = Math.Max(1, maxDepth)
    End Sub

    Public ReadOnly Property CanUndo As Boolean
        Get
            Return _undo.Count > 0
        End Get
    End Property

    Public ReadOnly Property CanRedo As Boolean
        Get
            Return _redo.Count > 0
        End Get
    End Property

    Public ReadOnly Property UndoCount As Integer
        Get
            Return _undo.Count
        End Get
    End Property

    Public ReadOnly Property RedoCount As Integer
        Get
            Return _redo.Count
        End Get
    End Property

    Public ReadOnly Property NextUndoDescription As String
        Get
            If _undo.Count = 0 Then Return Nothing
            Return _undo(_undo.Count - 1).Description
        End Get
    End Property

    Public ReadOnly Property NextRedoDescription As String
        Get
            If _redo.Count = 0 Then Return Nothing
            Return _redo(_redo.Count - 1).Description
        End Get
    End Property

    Public Sub Push(action As IUndoAction)
        If action Is Nothing Then Return
        _undo.Add(action)
        DiscardAll(_redo)
        _redo.Clear()
        While _undo.Count > _maxDepth
            DiscardAction(_undo(0))
            _undo.RemoveAt(0)
        End While
    End Sub

    Public Function Undo() As Boolean
        If _undo.Count = 0 Then Return False
        Dim action = _undo(_undo.Count - 1)
        _undo.RemoveAt(_undo.Count - 1)
        action.Undo()
        _redo.Add(action)
        Return True
    End Function

    Public Function Redo() As Boolean
        If _redo.Count = 0 Then Return False
        Dim action = _redo(_redo.Count - 1)
        _redo.RemoveAt(_redo.Count - 1)
        action.Redo()
        _undo.Add(action)
        Return True
    End Function

    Public Sub Clear()
        DiscardAll(_undo)
        DiscardAll(_redo)
        _undo.Clear()
        _redo.Clear()
    End Sub

    Private Shared Sub DiscardAll(list As List(Of IUndoAction))
        For Each action In list
            DiscardAction(action)
        Next
    End Sub

    Private Shared Sub DiscardAction(action As IUndoAction)
        Dim disposable = TryCast(action, IDisposable)
        disposable?.Dispose()
    End Sub
End Class

''' <summary>
''' Snapshot of a screenshot box transform (position, frame size, zoom, pan).
''' </summary>
Public Structure BoxTransformState
    Public Location As Point
    Public Size As Size
    Public Zoom As Double
    Public Pan As Point

    Public Shared Function FromBox(box As MovableScreenshotBox) As BoxTransformState
        Return New BoxTransformState With {
            .Location = box.Location,
            .Size = box.Size,
            .Zoom = box.Zoom,
            .Pan = box.ContentPan
        }
    End Function

    Public Function EqualsState(other As BoxTransformState) As Boolean
        Return Location = other.Location AndAlso
            Size = other.Size AndAlso
            Math.Abs(Zoom - other.Zoom) < 0.0001 AndAlso
            Pan = other.Pan
    End Function
End Structure
