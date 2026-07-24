''' <summary>
''' Helpers for WM_MOUSEACTIVATE so the first click on an inactive window
''' both activates the app and performs the intended action (no double-click).
''' </summary>
Public Module MouseActivateHelper
    Public Const WM_MOUSEACTIVATE As Integer = &H21

    Public Const MA_ACTIVATE As Integer = 1
    Public Const MA_ACTIVATEANDEAT As Integer = 2
    Public Const MA_NOACTIVATE As Integer = 3
    Public Const MA_NOACTIVATEANDEAT As Integer = 4

    ''' <summary>
    ''' Converts "eat the mouse message" results into non-eating equivalents so the
    ''' click is delivered to buttons / screenshot controls after activation.
    ''' </summary>
    Public Function WithoutEatingClick(activateResult As Integer) As Integer
        Select Case activateResult
            Case MA_ACTIVATEANDEAT
                Return MA_ACTIVATE
            Case MA_NOACTIVATEANDEAT
                Return MA_NOACTIVATE
            Case Else
                Return activateResult
        End Select
    End Function

    ''' <summary>
    ''' If <paramref name="m"/> is WM_MOUSEACTIVATE, rewrite Result so the click is not eaten.
    ''' Returns True when the message was handled (caller should not re-process).
    ''' </summary>
    Public Function ApplyNoEatResult(ByRef m As Message) As Boolean
        If m.Msg <> WM_MOUSEACTIVATE Then Return False
        Dim current = CInt(m.Result.ToInt64() And &HFFFFFFFFL)
        Dim fixedResult = WithoutEatingClick(current)
        If fixedResult <> current Then
            m.Result = New IntPtr(fixedResult)
        End If
        Return True
    End Function

    ''' <summary>
    ''' Preferred result when we always want activation + pass-through click.
    ''' </summary>
    Public ReadOnly Property ActivateAndPassClick As IntPtr
        Get
            Return New IntPtr(MA_ACTIVATE)
        End Get
    End Property
End Module
