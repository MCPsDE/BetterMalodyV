using System.Drawing.Drawing2D;

namespace BetterMalodyV.Windows;

internal sealed class ActionButton : Button
{
    private bool hovered;
    private bool pressed;
    private bool sizing;
    public bool Primary { get; init; }
    public bool Danger { get; init; }

    public ActionButton()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
    }
    public Size RequiredContentSize()
    {
        var scale = DeviceDpi / 96f;
        var flags = TextFormatFlags.SingleLine | TextFormatFlags.NoPadding;
        flags |= UseMnemonic ? TextFormatFlags.HidePrefix : TextFormatFlags.NoPrefix;
        var text = TextRenderer.MeasureText(Text, Font, Size.Empty, flags);
        return new Size(text.Width + (int)Math.Ceiling(26 * scale) + Padding.Horizontal,
            Math.Max((int)Math.Ceiling(32 * scale), text.Height + (int)Math.Ceiling(12 * scale) + Padding.Vertical));
    }
    public override Size GetPreferredSize(Size proposedSize)
    {
        var required = RequiredContentSize();
        return new Size(Math.Max(required.Width, MinimumSize.Width), Math.Max(required.Height, MinimumSize.Height));
    }
    private void RefreshTextSize()
    {
        if (sizing) return;
        sizing = true;
        try
        {
            var required = RequiredContentSize();
            MinimumSize = new Size(Math.Max(MinimumSize.Width, required.Width), Math.Max(MinimumSize.Height, required.Height));
        }
        finally { sizing = false; }
        Parent?.PerformLayout(); Invalidate();
    }
    protected override void OnTextChanged(EventArgs e) { base.OnTextChanged(e); RefreshTextSize(); }
    protected override void OnFontChanged(EventArgs e) { base.OnFontChanged(e); RefreshTextSize(); }
    protected override void OnPaddingChanged(EventArgs e) { base.OnPaddingChanged(e); RefreshTextSize(); }
    protected override void OnDpiChangedAfterParent(EventArgs e) { base.OnDpiChangedAfterParent(e); RefreshTextSize(); }
    protected override void OnMouseEnter(EventArgs e) { hovered = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { hovered = pressed = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs e) { pressed = true; Invalidate(); base.OnMouseDown(e); }
    protected override void OnMouseUp(MouseEventArgs e) { pressed = false; Invalidate(); base.OnMouseUp(e); }
    protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }
    protected override void OnPaint(PaintEventArgs e)
    {
        var ancestor = Parent;
        while (ancestor is not null && ancestor.BackColor.A != 255) ancestor = ancestor.Parent;
        e.Graphics.Clear(ancestor?.BackColor ?? AppVisuals.Canvas);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var scale = DeviceDpi / 96f;
        var rect = new RectangleF(0.5f, 0.5f, Width-1, Height-1);
        using var path = Rounded(rect, 4*scale);
        var fill = !Enabled
            ? AppVisuals.SurfaceAlt
            : pressed
                ? (Primary ? AppVisuals.AccentPressed
                    : Danger ? AppVisuals.DangerPressed
                    : AppVisuals.ControlPressed)
                : hovered
                    ? (Primary ? AppVisuals.AccentHover
                        : Danger ? AppVisuals.DangerHover
                        : AppVisuals.SurfaceAlt)
                    : Primary ? AppVisuals.AccentFill : Danger ? AppVisuals.DangerFill : BackColor;
        var ink = !Enabled
            ? AppVisuals.MutedInk
            : Primary || Danger ? Color.White : ForeColor;
        using var brush = new SolidBrush(fill);
        e.Graphics.FillPath(brush,path);
        if (!Primary && !Danger && FlatAppearance.BorderSize != 0)
        {
            using var pen = new Pen(AppVisuals.Line,scale);
            e.Graphics.DrawPath(pen,path);
        }
        var inset = (int)Math.Ceiling(12 * scale);
        var textRect = Rectangle.FromLTRB(inset + Padding.Left, 2 + Padding.Top, Width - inset - Padding.Right, Height - 2 - Padding.Bottom);
        var flags = TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding;
        flags |= TextAlign == ContentAlignment.MiddleLeft ? TextFormatFlags.Left : TextFormatFlags.HorizontalCenter;
        if (!UseMnemonic) flags |= TextFormatFlags.NoPrefix;
        if (!ShowKeyboardCues) flags |= TextFormatFlags.HidePrefix;
        TextRenderer.DrawText(e.Graphics,Text,Font,textRect,ink,flags);
        if (Focused && ShowFocusCues) ControlPaint.DrawFocusRectangle(e.Graphics,Rectangle.Inflate(ClientRectangle,-4,-4),ink,fill);
    }
    private static GraphicsPath Rounded(RectangleF rect, float radius)
    {
        var path = new GraphicsPath();
        var diameter = Math.Min(radius*2,Math.Min(rect.Width,rect.Height));
        path.AddArc(rect.X,rect.Y,diameter,diameter,180,90);
        path.AddArc(rect.Right-diameter,rect.Y,diameter,diameter,270,90);
        path.AddArc(rect.Right-diameter,rect.Bottom-diameter,diameter,diameter,0,90);
        path.AddArc(rect.X,rect.Bottom-diameter,diameter,diameter,90,90);
        path.CloseFigure();
        return path;
    }
}
