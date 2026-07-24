''' <summary>
''' A single screenshot item on a tab canvas (position + size; image held by UI).
''' Pure model — no WinForms control references.
''' </summary>
Public Class ScreenshotItem
    Public Sub New(Optional location As Point = Nothing, Optional size As Size = Nothing)
        Id = Guid.NewGuid()
        Me.Location = location
        Me.Size = size
    End Sub

    Public ReadOnly Property Id As Guid
    Public Property Location As Point
    Public Property Size As Size
End Class

''' <summary>
''' Independent collection of screenshots for one tab.
''' </summary>
Public Class TabSession
    Private ReadOnly _items As New List(Of ScreenshotItem)()

    Public Sub New(Optional name As String = "Tab")
        Me.Name = name
    End Sub

    Public Property Name As String

    Public ReadOnly Property Items As IReadOnlyList(Of ScreenshotItem)
        Get
            Return _items
        End Get
    End Property

    Public Function AddScreenshot(location As Point, size As Size) As ScreenshotItem
        Dim item As New ScreenshotItem(location, size)
        _items.Add(item)
        Return item
    End Function

    Public Function TryGetItem(id As Guid) As ScreenshotItem
        Return _items.FirstOrDefault(Function(i) i.Id = id)
    End Function

    Public Function MoveScreenshot(id As Guid, newLocation As Point) As Boolean
        Dim item = TryGetItem(id)
        If item Is Nothing Then
            Return False
        End If
        item.Location = newLocation
        Return True
    End Function

    Public Function RemoveScreenshot(id As Guid) As Boolean
        Dim item = TryGetItem(id)
        If item Is Nothing Then
            Return False
        End If
        Return _items.Remove(item)
    End Function
End Class

''' <summary>
''' Multi-tab workspace: each tab has an isolated screenshot collection.
''' </summary>
Public Class WorkspaceModel
    Private ReadOnly _tabs As New List(Of TabSession)()
    Private _activeIndex As Integer = -1

    Public ReadOnly Property Tabs As IReadOnlyList(Of TabSession)
        Get
            Return _tabs
        End Get
    End Property

    Public ReadOnly Property ActiveTab As TabSession
        Get
            If _activeIndex < 0 OrElse _activeIndex >= _tabs.Count Then
                Return Nothing
            End If
            Return _tabs(_activeIndex)
        End Get
    End Property

    Public Property ActiveTabIndex As Integer
        Get
            Return _activeIndex
        End Get
        Set(value As Integer)
            If value < -1 OrElse value >= _tabs.Count Then
                Throw New ArgumentOutOfRangeException(NameOf(value))
            End If
            _activeIndex = value
        End Set
    End Property

    Public Function AddTab(Optional name As String = Nothing) As TabSession
        Dim tabName = If(String.IsNullOrWhiteSpace(name),
            TabNamingHelper.NextDefaultTabName(_tabs.Count),
            name.Trim())
        Dim tab As New TabSession(tabName)
        _tabs.Add(tab)
        _activeIndex = _tabs.Count - 1
        Return tab
    End Function

    ''' <summary>
    ''' Renames a tab by index. Returns False if index is invalid or name is blank.
    ''' </summary>
    Public Function RenameTabAt(index As Integer, newName As String) As Boolean
        If index < 0 OrElse index >= _tabs.Count Then Return False
        Dim normalized = TabNamingHelper.NormalizeTabName(newName)
        If normalized Is Nothing Then Return False
        _tabs(index).Name = normalized
        Return True
    End Function

    Public Function RemoveTabAt(index As Integer) As Boolean
        If index < 0 OrElse index >= _tabs.Count Then
            Return False
        End If
        _tabs.RemoveAt(index)
        If _tabs.Count = 0 Then
            _activeIndex = -1
        ElseIf _activeIndex >= _tabs.Count Then
            _activeIndex = _tabs.Count - 1
        End If
        Return True
    End Function
End Class
