using System.Globalization;

namespace BetterMalodyV.Windows;

/// <summary>Native numeric editing with theme-painted horizontal step buttons.</summary>
public sealed class NumberField : NumericUpDown
{
    private readonly StepButton minus;
    private readonly StepButton plus;
    private bool arranging;

    public NumberField()
    {
        BorderStyle = BorderStyle.None;
        BackColor = AppVisuals.Surface; ForeColor = AppVisuals.Ink;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        minus = new StepButton(this, false); plus = new StepButton(this, true);
        Controls.Add(minus); Controls.Add(plus);
        foreach (var edit in Controls.OfType<TextBox>())
        { edit.GotFocus += (_, _) => Invalidate(); edit.LostFocus += (_, _) => Invalidate(); }
        RefreshMetrics();
    }

    public override Size GetPreferredSize(Size proposedSize) => RequiredSize();

    private Size RequiredSize()
    {
        var format = ThousandsSeparator ? "N" + DecimalPlaces : "F" + DecimalPlaces;
        var width = new[] { Minimum, Maximum, Value }.Max(v => TextRenderer.MeasureText(v.ToString(format, CultureInfo.CurrentCulture), Font,
            Size.Empty, TextFormatFlags.SingleLine | TextFormatFlags.NoPadding).Width);
        return new Size(width + Px(56), Math.Max(Px(32), Font.Height + Px(12)));
    }

    private int Px(int value) => (int)Math.Ceiling(value * DeviceDpi / 96f);
    private void RefreshMetrics()
    {
        if (minus == null || arranging) return;
        var size = RequiredSize();
        MinimumSize = size; Height = size.Height;
        PerformLayout(); Invalidate();
    }

    protected override void OnLayout(LayoutEventArgs e)
    {
        if (arranging) return;
        base.OnLayout(e);
        if (minus == null || arranging) return;
        arranging = true;
        try
        {
            foreach (Control child in Controls)
                if (child.GetType().Name.Contains("UpDownButtons", StringComparison.Ordinal)) { child.Visible = false; child.TabStop = false; }
            var width = Px(22); var inset = Px(1);
            plus.SetBounds(ClientSize.Width - width - inset, inset, width, Math.Max(1, ClientSize.Height - inset * 2));
            minus.SetBounds(plus.Left - width, inset, width, plus.Height);
            foreach (var edit in Controls.OfType<TextBox>())
            {
                edit.BorderStyle = BorderStyle.None;
                edit.SetBounds(Px(6), Math.Max(inset, (ClientSize.Height - edit.PreferredHeight) / 2), Math.Max(1, minus.Left - Px(10)), edit.PreferredHeight);
            }
            minus.Enabled = Enabled && !ReadOnly && Value > Minimum;
            plus.Enabled = Enabled && !ReadOnly && Value < Maximum;
            minus.BringToFront(); plus.BringToFront();
        }
        finally { arranging = false; }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(Enabled ? AppVisuals.Surface : AppVisuals.SurfaceAlt);
        using var line = new Pen(ContainsFocus ? AppVisuals.Accent : AppVisuals.Line);
        e.Graphics.DrawRectangle(line, 0, 0, Width - 1, Height - 1);
    }
    protected override void OnFontChanged(EventArgs e) { base.OnFontChanged(e); RefreshMetrics(); }
    protected override void OnDpiChangedAfterParent(EventArgs e) { base.OnDpiChangedAfterParent(e); RefreshMetrics(); }
    protected override void OnEnabledChanged(EventArgs e) { base.OnEnabledChanged(e); PerformLayout(); Invalidate(true); }
    protected override void OnValueChanged(EventArgs e) { base.OnValueChanged(e); PerformLayout(); Invalidate(true); }
    protected override void OnMouseWheel(MouseEventArgs e) { if (ContainsFocus) base.OnMouseWheel(e); }

    private sealed class StepButton : Button
    {
        private readonly NumberField owner;
        private readonly bool increase;
        private bool hovered;
        private bool pressed;
        public StepButton(NumberField owner, bool increase)
        {
            this.owner = owner; this.increase = increase;
            Text = ""; TabStop = false; Cursor = Cursors.Hand; FlatStyle = FlatStyle.Flat;
            AccessibleName = increase ? "增加数值" : "减少数值";
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        }
        protected override void OnClick(EventArgs e)
        {
            owner.Focus();
            if (owner.Enabled && !owner.ReadOnly) { if (increase) owner.UpButton(); else owner.DownButton(); }
            base.OnClick(e);
        }
        protected override void OnMouseEnter(EventArgs e) { hovered = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hovered = pressed = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { pressed = true; Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { pressed = false; Invalidate(); base.OnMouseUp(e); }
        protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(!Enabled ? AppVisuals.SurfaceAlt : pressed ? AppVisuals.ControlPressed : hovered ? AppVisuals.SurfaceAlt : AppVisuals.Surface);
            var scale = DeviceDpi / 96f;
            using var line = new Pen(Enabled ? AppVisuals.Ink : AppVisuals.MutedInk, Math.Max(1, scale));
            var cx = Width / 2f; var cy = Height / 2f; var radius = 4 * scale;
            e.Graphics.DrawLine(line, cx - radius, cy, cx + radius, cy);
            if (increase) e.Graphics.DrawLine(line, cx, cy - radius, cx, cy + radius);
        }
    }
}
