Imports System.Drawing.Drawing2D

''' <summary>
''' Tab control with modern dark pill chrome (browser-style tabs, circular close, circular +).
''' </summary>
Public Class ModernTabControl
    Inherits TabControl

    Public Event RequestNewTab As EventHandler
    Public Event RequestCloseTab As EventHandler(Of TabCloseEventArgs)

    ' Chrome palette (dark rounded reference style)
    Private Shared ReadOnly StripColor As Color = Color.FromArgb(36, 36, 40)
    Private Shared ReadOnly TabFill As Color = Color.FromArgb(52, 52, 56)
    Private Shared ReadOnly TabFillSelected As Color = Color.FromArgb(64, 64, 70)
    Private Shared ReadOnly TabBorder As Color = Color.FromArgb(78, 78, 84)
    Private Shared ReadOnly TextPrimary As Color = Color.FromArgb(245, 245, 247)
    Private Shared ReadOnly TextMuted As Color = Color.FromArgb(170, 170, 176)
    Private Shared ReadOnly CloseCircle As Color = Color.FromArgb(78, 78, 84)
    Private Shared ReadOnly CloseCircleHover As Color = Color.FromArgb(118, 118, 126)
    Private Shared ReadOnly PlusCircle As Color = Color.FromArgb(52, 52, 56)
    Private Shared ReadOnly PlusCircleHover As Color = Color.FromArgb(72, 72, 78)
    Private Shared ReadOnly ContentBack As Color = Color.FromArgb(245, 245, 248)

    Private Const TabHeight As Integer = 36
    Private Const TabMinWidth As Integer = 112
    Private Const TabMaxWidth As Integer = 200
    Private Const PillPadX As Integer = 3
    Private Const PillPadY As Integer = 5
    Private Const CloseDiameter As Integer = 18
    Private Const PlusDiameter As Integer = 28
    Private Const CloseHitPad As Integer = 4
    Private Const PlusGap As Integer = 8

    Private _hoverCloseIndex As Integer = -1
    Private _hoverPlus As Boolean
    Private _tabFont As Font

    Public Sub New()
        SetStyle(ControlStyles.OptimizedDoubleBuffer Or
                 ControlStyles.AllPaintingInWmPaint Or
                 ControlStyles.ResizeRedraw Or
                 ControlStyles.Selectable, True)
        UpdateStyles()

        DrawMode = TabDrawMode.OwnerDrawFixed
        SizeMode = TabSizeMode.Fixed
        ItemSize = New Size(140, TabHeight)
        Multiline = False
        Padding = New Point(16, 6)
        HotTrack = True
        BackColor = StripColor
        _tabFont = New Font("Segoe UI", 9.0F, FontStyle.Regular, GraphicsUnit.Point)
    End Sub

    Protected Overrides Sub Dispose(disposing As Boolean)
        If disposing Then
            _tabFont?.Dispose()
            _tabFont = Nothing
        End If
        MyBase.Dispose(disposing)
    End Sub

    ''' <summary>
    ''' Which tab header contains the point, or -1. Ignores the + button area.
    ''' </summary>
    Public Function HitTestTabIndex(clientPoint As Point) As Integer
        If GetPlusButtonRect().Contains(clientPoint) Then Return -1
        For i = 0 To TabCount - 1
            If GetTabRect(i).Contains(clientPoint) Then Return i
        Next
        Return -1
    End Function

    ''' <summary>Close-button hit area for a tab (includes padding).</summary>
    Public Function GetCloseButtonRect(index As Integer) As Rectangle
        If index < 0 OrElse index >= TabCount Then Return Rectangle.Empty
        Dim glyph = GetCloseGlyphRect(GetPillRect(GetTabRect(index)))
        Return New Rectangle(
            glyph.X - CloseHitPad,
            glyph.Y - CloseHitPad,
            glyph.Width + CloseHitPad * 2,
            glyph.Height + CloseHitPad * 2)
    End Function

    ''' <summary>Circular + control to the right of the last tab.</summary>
    Public Function GetPlusButtonRect() As Rectangle
        Dim d = PlusDiameter
        Dim y As Integer
        Dim x As Integer
        If TabCount = 0 Then
            x = 10
            y = Math.Max(4, (TabHeight - d) \ 2 + 2)
        Else
            Dim last = GetTabRect(TabCount - 1)
            x = last.Right + PlusGap
            y = last.Top + (last.Height - d) \ 2
        End If
        Return New Rectangle(x, y, d, d)
    End Function

    ''' <summary>
    ''' Resize fixed tab slots from current titles.
    ''' </summary>
    Public Sub UpdateTabItemSize()
        Dim contentW = TabMinWidth
        Using g = CreateGraphics()
            For Each page As TabPage In TabPages
                Dim textW = TextRenderer.MeasureText(g, page.Text, _tabFont).Width
                contentW = Math.Max(contentW, Math.Min(TabMaxWidth, textW + 56))
            Next
        End Using
        Dim nextSize As New Size(contentW, TabHeight)
        If ItemSize <> nextSize Then
            ItemSize = nextSize
        End If
        Invalidate()
    End Sub

    Protected Overrides Sub OnDrawItem(e As DrawItemEventArgs)
        If e.Index < 0 OrElse e.Index >= TabCount Then Return
        Dim g = e.Graphics
        g.SmoothingMode = SmoothingMode.AntiAlias
        g.PixelOffsetMode = PixelOffsetMode.HighQuality
        g.TextRenderingHint = Drawing.Text.TextRenderingHint.ClearTypeGridFit

        ' Fill the raw tab slot with strip color first (covers stock theme)
        Using stripBrush As New SolidBrush(StripColor)
            g.FillRectangle(stripBrush, e.Bounds)
        End Using

        DrawContentTab(g, e.Index)
    End Sub

    ''' <summary>
    ''' Paint strip background, gaps, and the circular + after the default tab paint cycle.
    ''' </summary>
    Protected Overrides Sub WndProc(ByRef m As Message)
        MyBase.WndProc(m)
        If m.Msg = &HF Then ' WM_PAINT
            PaintStripChrome()
        End If
    End Sub

    Private Sub PaintStripChrome()
        If Not IsHandleCreated OrElse TabCount < 0 Then Return
        Try
            Using g = CreateGraphics()
                g.SmoothingMode = SmoothingMode.AntiAlias
                g.PixelOffsetMode = PixelOffsetMode.HighQuality

                ' Header strip only (above page content)
                Dim headerHeight = If(TabCount > 0, GetTabRect(0).Bottom + 2, TabHeight + 4)
                Dim headerRect As New Rectangle(0, 0, ClientSize.Width, Math.Min(headerHeight, ClientSize.Height))

                ' Fill gaps between / around pills without covering page body
                Using stripBrush As New SolidBrush(StripColor)
                    ' Left margin
                    If TabCount > 0 Then
                        Dim first = GetTabRect(0)
                        If first.Left > 0 Then
                            g.FillRectangle(stripBrush, 0, 0, first.Left, headerRect.Height)
                        End If
                        ' Between tabs (fixed mode usually none) + right of last tab through + and beyond
                        Dim last = GetTabRect(TabCount - 1)
                        Dim rightStart = last.Right
                        If rightStart < ClientSize.Width Then
                            g.FillRectangle(stripBrush, rightStart, 0, ClientSize.Width - rightStart, headerRect.Height)
                        End If
                        ' Thin band under the tab slots but above content
                        Dim underY = last.Bottom
                        If underY < headerRect.Height Then
                            g.FillRectangle(stripBrush, 0, underY, ClientSize.Width, headerRect.Height - underY)
                        End If
                    Else
                        g.FillRectangle(stripBrush, headerRect)
                    End If
                End Using

                DrawPlusButton(g)
            End Using
        Catch
            ' Ignore paint races during dispose / handle recreation
        End Try
    End Sub

    Private Sub DrawContentTab(g As Graphics, index As Integer)
        Dim page = TabPages(index)
        Dim selected = (index = SelectedIndex)
        Dim pill = GetPillRect(GetTabRect(index))
        Dim fill = If(selected, TabFillSelected, TabFill)
        Dim textColor = If(selected, TextPrimary, TextMuted)

        Using path = CreateStadiumPath(pill)
            Using brush As New SolidBrush(fill)
                g.FillPath(brush, path)
            End Using
            If selected Then
                Using pen As New Pen(TabBorder)
                    g.DrawPath(pen, path)
                End Using
            End If
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

        Dim closeFill = If(_hoverCloseIndex = index, CloseCircleHover, CloseCircle)
        Using brush As New SolidBrush(closeFill)
            g.FillEllipse(brush, closeRect)
        End Using

        Dim cx = closeRect.X + closeRect.Width / 2.0F
        Dim cy = closeRect.Y + closeRect.Height / 2.0F
        Dim arm = 3.6F
        Using pen As New Pen(TextPrimary, 1.6F)
            pen.StartCap = LineCap.Round
            pen.EndCap = LineCap.Round
            g.DrawLine(pen, cx - arm, cy - arm, cx + arm, cy + arm)
            g.DrawLine(pen, cx + arm, cy - arm, cx - arm, cy + arm)
        End Using
    End Sub

    Private Sub DrawPlusButton(g As Graphics)
        Dim circle = GetPlusButtonRect()
        If circle.IsEmpty Then Return

        Dim fill = If(_hoverPlus, PlusCircleHover, PlusCircle)
        Using brush As New SolidBrush(fill)
            g.FillEllipse(brush, circle)
        End Using
        Using pen As New Pen(TabBorder)
            g.DrawEllipse(pen, circle)
        End Using

        Dim cx = circle.X + circle.Width / 2.0F
        Dim cy = circle.Y + circle.Height / 2.0F
        Dim arm = 6.0F
        Using pen As New Pen(TextPrimary, 1.9F)
            pen.StartCap = LineCap.Round
            pen.EndCap = LineCap.Round
            g.DrawLine(pen, cx - arm, cy, cx + arm, cy)
            g.DrawLine(pen, cx, cy - arm, cx, cy + arm)
        End Using
    End Sub

    Private Shared Function GetPillRect(tabBounds As Rectangle) As Rectangle
        Return New Rectangle(
            tabBounds.X + PillPadX,
            tabBounds.Y + PillPadY,
            Math.Max(24, tabBounds.Width - PillPadX * 2 - 2),
            Math.Max(20, tabBounds.Height - PillPadY * 2 - 1))
    End Function

    Private Shared Function GetCloseGlyphRect(pill As Rectangle) As Rectangle
        Dim d = CloseDiameter
        Dim x = pill.Right - d - 8
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
        Dim arc As New RectangleF(bounds.X, bounds.Y, diameter, diameter)
        path.AddArc(arc, 90, 180)
        arc.X = bounds.Right - diameter
        path.AddArc(arc, 270, 180)
        path.CloseFigure()
        Return path
    End Function

    Protected Overrides Sub OnMouseMove(e As MouseEventArgs)
        MyBase.OnMouseMove(e)
        Dim closeHover = -1
        Dim plusHover = GetPlusButtonRect().Contains(e.Location)
        If Not plusHover Then
            Dim idx = HitTestTabIndex(e.Location)
            If idx >= 0 AndAlso GetCloseButtonRect(idx).Contains(e.Location) Then
                closeHover = idx
            End If
        End If

        If closeHover <> _hoverCloseIndex OrElse plusHover <> _hoverPlus Then
            _hoverCloseIndex = closeHover
            _hoverPlus = plusHover
            Invalidate()
        End If

        Cursor = If(plusHover OrElse closeHover >= 0, Cursors.Hand, Cursors.Default)
    End Sub

    Protected Overrides Sub OnMouseLeave(e As EventArgs)
        MyBase.OnMouseLeave(e)
        If _hoverCloseIndex <> -1 OrElse _hoverPlus Then
            _hoverCloseIndex = -1
            _hoverPlus = False
            Invalidate()
        End If
        Cursor = Cursors.Default
    End Sub

    Protected Overrides Sub OnMouseDown(e As MouseEventArgs)
        If e.Button = MouseButtons.Left Then
            If GetPlusButtonRect().Contains(e.Location) Then
                RaiseEvent RequestNewTab(Me, EventArgs.Empty)
                Return
            End If
            Dim idx = HitTestTabIndex(e.Location)
            If idx >= 0 AndAlso GetCloseButtonRect(idx).Contains(e.Location) Then
                RaiseEvent RequestCloseTab(Me, New TabCloseEventArgs(idx))
                Return
            End If
        End If
        MyBase.OnMouseDown(e)
    End Sub

    Protected Overrides Sub OnSelectedIndexChanged(e As EventArgs)
        MyBase.OnSelectedIndexChanged(e)
        Invalidate()
    End Sub

    Protected Overrides Sub OnControlAdded(e As ControlEventArgs)
        MyBase.OnControlAdded(e)
        Dim page = TryCast(e.Control, TabPage)
        If page IsNot Nothing Then
            page.BackColor = ContentBack
            page.UseVisualStyleBackColor = False
        End If
        UpdateTabItemSize()
    End Sub

    Protected Overrides Sub OnControlRemoved(e As ControlEventArgs)
        MyBase.OnControlRemoved(e)
        UpdateTabItemSize()
    End Sub
End Class

''' <summary>Close request for a specific tab index.</summary>
Public Class TabCloseEventArgs
    Inherits EventArgs

    Public Sub New(tabIndex As Integer)
        Me.TabIndex = tabIndex
    End Sub

    Public ReadOnly Property TabIndex As Integer
End Class
