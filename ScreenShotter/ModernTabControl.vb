Imports System.Drawing.Drawing2D
Imports System.Runtime.InteropServices

''' <summary>
''' Fluid capsule tabs. Fully paints the header (no stock square tab slots) and
''' removes the unthemed client-edge border that otherwise shows as a black frame.
''' </summary>
Public Class ModernTabControl
    Inherits TabControl

    Public Event RequestNewTab As EventHandler
    Public Event RequestCloseTab As EventHandler(Of TabCloseEventArgs)

    Private Shared ReadOnly StripColor As Color = Color.FromArgb(245, 245, 248)
    Private Shared ReadOnly TabIdle As Color = Color.FromArgb(228, 228, 234)
    Private Shared ReadOnly TabHover As Color = Color.FromArgb(218, 218, 226)
    Private Shared ReadOnly TabSelected As Color = Color.White
    Private Shared ReadOnly BorderIdle As Color = Color.FromArgb(210, 210, 218)
    Private Shared ReadOnly BorderSelected As Color = Color.FromArgb(190, 190, 200)
    Private Shared ReadOnly Shadow As Color = Color.FromArgb(28, 60, 60, 70)
    Private Shared ReadOnly TextPrimary As Color = Color.FromArgb(36, 36, 40)
    Private Shared ReadOnly TextMuted As Color = Color.FromArgb(120, 120, 128)
    Private Shared ReadOnly CloseIdle As Color = Color.FromArgb(210, 210, 218)
    Private Shared ReadOnly CloseHover As Color = Color.FromArgb(190, 190, 200)
    Private Shared ReadOnly PlusFill As Color = Color.FromArgb(228, 228, 234)
    Private Shared ReadOnly PlusFillHover As Color = Color.FromArgb(210, 210, 218)
    Private Shared ReadOnly ContentBack As Color = Color.FromArgb(245, 245, 248)
    Private Shared ReadOnly Hairline As Color = Color.FromArgb(220, 220, 226)

    Private Const TabHeight As Integer = 40
    Private Const TabMinWidth As Integer = 118
    Private Const TabMaxWidth As Integer = 210
    Private Const PillInsetX As Integer = 7
    Private Const PillInsetY As Integer = 6
    Private Const CloseDiameter As Integer = 18
    Private Const PlusDiameter As Integer = 28
    Private Const CloseHitPad As Integer = 4
    Private Const PlusGap As Integer = 8

    Private _hoverTabIndex As Integer = -1
    Private _hoverCloseIndex As Integer = -1
    Private _tabFont As Font
    Private ReadOnly _plusButton As New PlusCircleButton()
    Private _plusParent As Control

    <DllImport("uxtheme.dll", CharSet:=CharSet.Unicode)>
    Private Shared Function SetWindowTheme(hWnd As IntPtr, pszSubAppName As String, pszSubIdList As String) As Integer
    End Function

    Public Sub New()
        ' UserPaint: we own the entire control surface (header + page frame).
        ' That is what removes the square “slot” indent behind each capsule.
        SetStyle(ControlStyles.UserPaint Or
                 ControlStyles.AllPaintingInWmPaint Or
                 ControlStyles.OptimizedDoubleBuffer Or
                 ControlStyles.ResizeRedraw Or
                 ControlStyles.Selectable, True)
        UpdateStyles()

        Appearance = TabAppearance.Normal
        DrawMode = TabDrawMode.OwnerDrawFixed
        SizeMode = TabSizeMode.Fixed
        ItemSize = New Size(140, TabHeight)
        Multiline = False
        Padding = New Point(18, 8)
        HotTrack = False
        BackColor = StripColor
        _tabFont = New Font("Segoe UI Semibold", 9.0F, FontStyle.Regular, GraphicsUnit.Point)

        _plusButton.Size = New Size(PlusDiameter, PlusDiameter)
        _plusButton.TabStop = False
        _plusButton.Cursor = Cursors.Hand
        _plusButton.Visible = False
        AddHandler _plusButton.Click, AddressOf OnPlusButtonClick
    End Sub

    ''' <summary>
    ''' Drop WS_EX_CLIENTEDGE / WS_BORDER so unthemed TabControl does not draw a black frame.
    ''' </summary>
    Protected Overrides ReadOnly Property CreateParams As CreateParams
        Get
            Dim cp = MyBase.CreateParams
            Const WS_EX_CLIENTEDGE As Integer = &H200
            Const WS_EX_STATICEDGE As Integer = &H20000
            Const WS_BORDER As Integer = &H800000
            cp.ExStyle = cp.ExStyle And Not (WS_EX_CLIENTEDGE Or WS_EX_STATICEDGE)
            cp.Style = cp.Style And Not WS_BORDER
            Return cp
        End Get
    End Property

    Protected Overrides Sub OnHandleCreated(e As EventArgs)
        MyBase.OnHandleCreated(e)
        Try
            SetWindowTheme(Handle, "", "")
        Catch
        End Try
    End Sub

    Protected Overrides Sub Dispose(disposing As Boolean)
        If disposing Then
            RemoveHandler _plusButton.Click, AddressOf OnPlusButtonClick
            DetachPlusButton()
            _plusButton.Dispose()
            _tabFont?.Dispose()
            _tabFont = Nothing
        End If
        MyBase.Dispose(disposing)
    End Sub

    Private Sub OnPlusButtonClick(sender As Object, e As EventArgs)
        RaiseEvent RequestNewTab(Me, EventArgs.Empty)
    End Sub

    Public Function HitTestTabIndex(clientPoint As Point) As Integer
        For i = 0 To TabCount - 1
            If GetTabRect(i).Contains(clientPoint) Then Return i
        Next
        Return -1
    End Function

    Public Function GetCloseButtonRect(index As Integer) As Rectangle
        If index < 0 OrElse index >= TabCount Then Return Rectangle.Empty
        Dim glyph = GetCloseGlyphRect(GetPillRect(GetTabRect(index)))
        Return New Rectangle(
            glyph.X - CloseHitPad,
            glyph.Y - CloseHitPad,
            glyph.Width + CloseHitPad * 2,
            glyph.Height + CloseHitPad * 2)
    End Function

    Public Function GetPlusButtonRect() As Rectangle
        Dim d = PlusDiameter
        Dim x As Integer
        Dim y As Integer
        If TabCount = 0 Then
            x = 10
            y = Math.Max(4, (TabHeight - d) \ 2)
        Else
            Dim last = GetTabRect(TabCount - 1)
            x = last.Right + PlusGap
            y = last.Top + (last.Height - d) \ 2
        End If
        Return New Rectangle(x, y, d, d)
    End Function

    Public Sub UpdateTabItemSize()
        Dim contentW = TabMinWidth
        If IsHandleCreated Then
            Using g = CreateGraphics()
                For Each page As TabPage In TabPages
                    Dim textW = TextRenderer.MeasureText(g, page.Text, _tabFont).Width
                    contentW = Math.Max(contentW, Math.Min(TabMaxWidth, textW + 64))
                Next
            End Using
        End If
        Dim nextSize As New Size(contentW, TabHeight)
        If ItemSize <> nextSize Then
            ItemSize = nextSize
        End If
        PositionPlusButton()
        Invalidate()
    End Sub

    Protected Overrides Sub OnPaintBackground(e As PaintEventArgs)
        ' Fully painted in OnPaint — prevents system square slots / black erase
    End Sub

    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        Dim g = e.Graphics
        g.SmoothingMode = SmoothingMode.AntiAlias
        g.PixelOffsetMode = PixelOffsetMode.HighQuality
        g.CompositingQuality = CompositingQuality.HighQuality
        g.TextRenderingHint = Drawing.Text.TextRenderingHint.ClearTypeGridFit

        ' Entire surface same light color (no black frame, no square tab wells)
        Using strip As New SolidBrush(StripColor)
            g.FillRectangle(strip, ClientRectangle)
        End Using

        ' Page body area (children still paint on top)
        Dim pageRect = DisplayRectangle
        If pageRect.Width > 0 AndAlso pageRect.Height > 0 Then
            Using content As New SolidBrush(ContentBack)
                g.FillRectangle(content, pageRect)
            End Using
        End If

        ' Soft separator under the tab strip only (not a full-window border)
        Dim headerBottom = If(TabCount > 0, GetTabRect(0).Bottom, TabHeight)
        Using pen As New Pen(Hairline)
            g.DrawLine(pen, 0, headerBottom, ClientSize.Width, headerBottom)
        End Using

        ' Capsules only — nothing square behind them
        For i = 0 To TabCount - 1
            DrawCapsuleTab(g, i, GetTabRect(i))
        Next

        PositionPlusButton()
    End Sub

    Protected Overrides Sub WndProc(ByRef m As Message)
        Const WM_LBUTTONDOWN As Integer = &H201
        Const WM_ERASEBKGND As Integer = &H14
        Const WM_NCPAINT As Integer = &H85

        If m.Msg = WM_LBUTTONDOWN Then
            Dim pt = LParamToPoint(m.LParam)
            Dim idx = HitTestTabIndex(pt)
            If idx >= 0 AndAlso GetCloseButtonRect(idx).Contains(pt) Then
                RaiseEvent RequestCloseTab(Me, New TabCloseEventArgs(idx))
                Return
            End If
        End If

        ' No system erase (avoids white/black rectangular flashes)
        If m.Msg = WM_ERASEBKGND Then
            m.Result = New IntPtr(1)
            Return
        End If

        ' Suppress non-client frame paint (black edge when unthemed)
        If m.Msg = WM_NCPAINT Then
            m.Result = IntPtr.Zero
            Return
        End If

        MyBase.WndProc(m)
    End Sub

    Private Shared Function LParamToPoint(lParam As IntPtr) As Point
        Dim lp = lParam.ToInt32()
        Dim x = CShort(lp And &HFFFF)
        Dim y = CShort((lp >> 16) And &HFFFF)
        Return New Point(x, y)
    End Function

    Private Sub DrawCapsuleTab(g As Graphics, index As Integer, tabBounds As Rectangle)
        Dim page = TabPages(index)
        Dim selected = (index = SelectedIndex)
        Dim hovered = (index = _hoverTabIndex)
        Dim pill = GetPillRect(tabBounds)

        Dim fill As Color
        Dim border As Color
        Dim textColor As Color
        If selected Then
            fill = TabSelected
            border = BorderSelected
            textColor = TextPrimary
        ElseIf hovered Then
            fill = TabHover
            border = BorderIdle
            textColor = TextPrimary
        Else
            fill = TabIdle
            border = BorderIdle
            textColor = TextMuted
        End If

        If selected Then
            Dim shadowRect As New Rectangle(pill.X, pill.Y + 1, pill.Width, pill.Height)
            Using shadowPath = CreateStadiumPath(shadowRect)
                Using brush As New SolidBrush(Shadow)
                    g.FillPath(brush, shadowPath)
                End Using
            End Using
        End If

        Using path = CreateStadiumPath(pill)
            Using brush As New SolidBrush(fill)
                g.FillPath(brush, path)
            End Using
            Using pen As New Pen(border, 1.0F)
                g.DrawPath(pen, path)
            End Using
        End Using

        Dim closeRect = GetCloseGlyphRect(pill)
        Dim textRect As New Rectangle(
            pill.Left + 14,
            pill.Top,
            Math.Max(8, closeRect.Left - pill.Left - 16),
            pill.Height)

        TextRenderer.DrawText(
            g,
            page.Text,
            _tabFont,
            textRect,
            textColor,
            TextFormatFlags.Left Or TextFormatFlags.VerticalCenter Or
            TextFormatFlags.EndEllipsis Or TextFormatFlags.NoPadding)

        Dim closeFill = If(_hoverCloseIndex = index, CloseHover, CloseIdle)
        Using brush As New SolidBrush(closeFill)
            g.FillEllipse(brush, closeRect)
        End Using

        Dim cx = closeRect.X + closeRect.Width / 2.0F
        Dim cy = closeRect.Y + closeRect.Height / 2.0F
        Dim arm = 3.4F
        Using pen As New Pen(textColor, 1.55F)
            pen.StartCap = LineCap.Round
            pen.EndCap = LineCap.Round
            g.DrawLine(pen, cx - arm, cy - arm, cx + arm, cy + arm)
            g.DrawLine(pen, cx + arm, cy - arm, cx - arm, cy + arm)
        End Using
    End Sub

    Private Shared Function GetPillRect(tabBounds As Rectangle) As Rectangle
        Dim h = Math.Max(22, tabBounds.Height - PillInsetY * 2)
        If (h And 1) = 1 Then h -= 1
        Dim w = Math.Max(h + 12, tabBounds.Width - PillInsetX * 2)
        Dim x = tabBounds.X + PillInsetX
        Dim y = tabBounds.Y + (tabBounds.Height - h) \ 2
        Return New Rectangle(x, y, w, h)
    End Function

    Private Shared Function GetCloseGlyphRect(pill As Rectangle) As Rectangle
        Dim d = CloseDiameter
        Dim x = pill.Right - d - 9
        Dim y = pill.Top + (pill.Height - d) \ 2
        Return New Rectangle(x, y, d, d)
    End Function

    Private Shared Function CreateStadiumPath(bounds As Rectangle) As GraphicsPath
        Dim path As New GraphicsPath()
        If bounds.Width <= 0 OrElse bounds.Height <= 0 Then
            path.AddRectangle(bounds)
            Return path
        End If

        Dim diameter = CSng(bounds.Height)
        If bounds.Width <= bounds.Height Then
            Dim side = Math.Min(bounds.Width, bounds.Height)
            Dim cx = bounds.X + (bounds.Width - side) \ 2
            Dim cy = bounds.Y + (bounds.Height - side) \ 2
            path.AddEllipse(cx, cy, side, side)
            Return path
        End If

        Dim leftCap As New RectangleF(bounds.X, bounds.Y, diameter, diameter)
        Dim rightCap As New RectangleF(bounds.Right - diameter, bounds.Y, diameter, diameter)
        path.AddArc(leftCap, 90.0F, 180.0F)
        path.AddArc(rightCap, 270.0F, 180.0F)
        path.CloseFigure()
        Return path
    End Function

    Protected Overrides Sub OnMouseMove(e As MouseEventArgs)
        MyBase.OnMouseMove(e)
        Dim tabHover = HitTestTabIndex(e.Location)
        Dim closeHover = -1
        If tabHover >= 0 AndAlso GetCloseButtonRect(tabHover).Contains(e.Location) Then
            closeHover = tabHover
        End If

        If tabHover <> _hoverTabIndex OrElse closeHover <> _hoverCloseIndex Then
            Dim dirty As New List(Of Integer)
            If _hoverTabIndex >= 0 Then dirty.Add(_hoverTabIndex)
            If tabHover >= 0 Then dirty.Add(tabHover)
            _hoverTabIndex = tabHover
            _hoverCloseIndex = closeHover
            For Each i In dirty.Distinct()
                If i >= 0 AndAlso i < TabCount Then Invalidate(GetTabRect(i))
            Next
        End If

        Cursor = If(closeHover >= 0 OrElse tabHover >= 0, Cursors.Hand, Cursors.Default)
    End Sub

    Protected Overrides Sub OnMouseLeave(e As EventArgs)
        MyBase.OnMouseLeave(e)
        Dim oldTab = _hoverTabIndex
        _hoverTabIndex = -1
        _hoverCloseIndex = -1
        If oldTab >= 0 AndAlso oldTab < TabCount Then Invalidate(GetTabRect(oldTab))
        Cursor = Cursors.Default
    End Sub

    Protected Overrides Sub OnSelectedIndexChanged(e As EventArgs)
        MyBase.OnSelectedIndexChanged(e)
        Invalidate()
        PositionPlusButton()
    End Sub

    Protected Overrides Sub OnControlAdded(e As ControlEventArgs)
        MyBase.OnControlAdded(e)
        Dim page = TryCast(e.Control, TabPage)
        If page IsNot Nothing Then
            page.BackColor = ContentBack
            page.UseVisualStyleBackColor = False
            page.BorderStyle = BorderStyle.None
        End If
        BeginInvoke(New Action(AddressOf UpdateTabItemSize))
    End Sub

    Protected Overrides Sub OnControlRemoved(e As ControlEventArgs)
        MyBase.OnControlRemoved(e)
        If IsHandleCreated Then
            BeginInvoke(New Action(AddressOf UpdateTabItemSize))
        End If
    End Sub

    Protected Overrides Sub OnParentChanged(e As EventArgs)
        MyBase.OnParentChanged(e)
        AttachPlusButton()
    End Sub

    Protected Overrides Sub OnVisibleChanged(e As EventArgs)
        MyBase.OnVisibleChanged(e)
        If _plusButton IsNot Nothing Then
            _plusButton.Visible = Visible AndAlso IsHandleCreated
        End If
        PositionPlusButton()
    End Sub

    Protected Overrides Sub OnSizeChanged(e As EventArgs)
        MyBase.OnSizeChanged(e)
        PositionPlusButton()
    End Sub

    Protected Overrides Sub OnLocationChanged(e As EventArgs)
        MyBase.OnLocationChanged(e)
        PositionPlusButton()
    End Sub

    Protected Overrides Sub OnLayout(levent As LayoutEventArgs)
        MyBase.OnLayout(levent)
        PositionPlusButton()
    End Sub

    Private Sub AttachPlusButton()
        DetachPlusButton()
        If Parent Is Nothing Then Return
        _plusParent = Parent
        If Not _plusParent.Controls.Contains(_plusButton) Then
            _plusParent.Controls.Add(_plusButton)
        End If
        _plusButton.Visible = True
        PositionPlusButton()
        _plusButton.BringToFront()
    End Sub

    Private Sub DetachPlusButton()
        If _plusParent IsNot Nothing AndAlso _plusParent.Controls.Contains(_plusButton) Then
            _plusParent.Controls.Remove(_plusButton)
        End If
        _plusParent = Nothing
    End Sub

    Private Sub PositionPlusButton()
        If _plusButton Is Nothing OrElse _plusParent Is Nothing Then Return
        If Not IsHandleCreated OrElse Not _plusParent.IsHandleCreated Then Return
        Try
            Dim local = GetPlusButtonRect()
            Dim screenPt = PointToScreen(local.Location)
            Dim parentPt = _plusParent.PointToClient(screenPt)
            Dim nextBounds As New Rectangle(parentPt, local.Size)
            If _plusButton.Bounds <> nextBounds Then
                _plusButton.Bounds = nextBounds
            End If
            If Not _plusButton.Visible AndAlso Visible Then
                _plusButton.Visible = True
            End If
            _plusButton.BringToFront()
        Catch
        End Try
    End Sub

    Private NotInheritable Class PlusCircleButton
        Inherits Control

        Private _hot As Boolean

        Public Sub New()
            SetStyle(ControlStyles.AllPaintingInWmPaint Or
                     ControlStyles.OptimizedDoubleBuffer Or
                     ControlStyles.UserPaint Or
                     ControlStyles.ResizeRedraw, True)
            BackColor = StripColor
        End Sub

        Protected Overrides Sub OnResize(e As EventArgs)
            MyBase.OnResize(e)
            ApplyCircleRegion()
        End Sub

        Protected Overrides Sub OnHandleCreated(e As EventArgs)
            MyBase.OnHandleCreated(e)
            ApplyCircleRegion()
        End Sub

        Private Sub ApplyCircleRegion()
            If Width <= 0 OrElse Height <= 0 Then Return
            Using path As New GraphicsPath()
                path.AddEllipse(0, 0, Width, Height)
                Dim old = Region
                Region = New Region(path)
                old?.Dispose()
            End Using
        End Sub

        Protected Overrides Sub OnMouseEnter(e As EventArgs)
            MyBase.OnMouseEnter(e)
            _hot = True
            Invalidate()
        End Sub

        Protected Overrides Sub OnMouseLeave(e As EventArgs)
            MyBase.OnMouseLeave(e)
            _hot = False
            Invalidate()
        End Sub

        Protected Overrides Sub OnPaint(e As PaintEventArgs)
            Dim g = e.Graphics
            g.SmoothingMode = SmoothingMode.AntiAlias
            Dim rect As New Rectangle(0, 0, Width - 1, Height - 1)
            Dim fill = If(_hot, PlusFillHover, PlusFill)
            Using brush As New SolidBrush(fill)
                g.FillEllipse(brush, rect)
            End Using
            Using pen As New Pen(BorderIdle)
                g.DrawEllipse(pen, rect)
            End Using
            Dim cx = Width / 2.0F
            Dim cy = Height / 2.0F
            Dim arm = Math.Max(5.0F, Width / 4.5F)
            Using pen As New Pen(TextPrimary, 1.8F)
                pen.StartCap = LineCap.Round
                pen.EndCap = LineCap.Round
                g.DrawLine(pen, cx - arm, cy, cx + arm, cy)
                g.DrawLine(pen, cx, cy - arm, cx, cy + arm)
            End Using
        End Sub
    End Class
End Class

Public Class TabCloseEventArgs
    Inherits EventArgs

    Public Sub New(tabIndex As Integer)
        Me.TabIndex = tabIndex
    End Sub

    Public ReadOnly Property TabIndex As Integer
End Class
