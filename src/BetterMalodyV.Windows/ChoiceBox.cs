namespace BetterMalodyV.Windows;

// Keep native keyboard navigation, dropdown and accessibility while drawing
// the closed field ourselves: Windows' ComboBox chrome otherwise stays white.
public sealed class ChoiceBox : ComboBox
{
    public ChoiceBox()
    {
        DropDownStyle = ComboBoxStyle.DropDownList;
        DrawMode = DrawMode.OwnerDrawFixed;
        FlatStyle = FlatStyle.Flat;
        ItemHeight = 25;
    }

    protected override void OnDrawItem(DrawItemEventArgs e)
    {
        if (e.Index < 0) return;
        var selected = (e.State & DrawItemState.Selected) != 0;
        using var fill = new SolidBrush(selected ? AppVisuals.AccentSoft : AppVisuals.Surface);
        e.Graphics.FillRectangle(fill, e.Bounds);
        TextRenderer.DrawText(e.Graphics, GetItemText(Items[e.Index]), Font,
            Rectangle.Inflate(e.Bounds, -6, 0), selected ? AppVisuals.Accent : AppVisuals.Ink,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        if ((e.State & DrawItemState.Focus) != 0) e.DrawFocusRectangle();
    }

    protected override void WndProc(ref Message m)
    {
        base.WndProc(ref m);
        if (m.Msg == 0x000F) // WM_PAINT
        {
            using var graphics = Graphics.FromHwnd(Handle);
            DrawField(graphics);
        }
        else if ((m.Msg == 0x0317 || m.Msg == 0x0318) && m.WParam != IntPtr.Zero) // WM_PRINT / WM_PRINTCLIENT
        {
            using var graphics = Graphics.FromHdc(m.WParam);
            DrawField(graphics);
        }
    }

    private void DrawField(Graphics graphics)
    {
        var bounds = ClientRectangle;
        if (bounds.Width <= 1 || bounds.Height <= 1) return;
        using var fill = new SolidBrush(AppVisuals.Surface);
        graphics.FillRectangle(fill, bounds);
        using var line = new Pen(Focused ? AppVisuals.Accent : AppVisuals.Line);
        graphics.DrawRectangle(line, 0, 0, bounds.Width - 1, bounds.Height - 1);
        var color = Enabled ? AppVisuals.Ink : AppVisuals.MutedInk;
        TextRenderer.DrawText(graphics, GetItemText(SelectedItem), Font,
            new Rectangle(8, 0, Math.Max(1, bounds.Width - 38), bounds.Height), color,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        using var chevron = new Pen(color, 1.5f);
        var x = bounds.Width - 18; var y = bounds.Height / 2;
        graphics.DrawLines(chevron, new[] { new Point(x - 4, y - 2), new Point(x, y + 2), new Point(x + 4, y - 2) });
    }
}
