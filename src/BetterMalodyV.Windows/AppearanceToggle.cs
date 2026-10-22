using System.Drawing.Drawing2D;

namespace BetterMalodyV.Windows;

/// <summary>A two-position appearance button with drawn sun and moon icons.</summary>
public sealed class AppearanceToggle : Button
{
    private bool darkSelected;
    private bool hovering;
    public event EventHandler? DarkSelectedChanged;

    public bool DarkSelected
    {
        get => darkSelected;
        set
        {
            if (darkSelected == value) return;
            darkSelected = value;
            AccessibleDescription = value ? "当前暗黑模式，点击切换亮色模式。" : "当前亮色模式，点击切换暗黑模式。";
            Invalidate();
            AccessibilityNotifyClients(AccessibleEvents.StateChange,-1);
            DarkSelectedChanged?.Invoke(this,EventArgs.Empty);
        }
    }

    public AppearanceToggle()
    {
        AccessibleName = "暗黑模式";
        AccessibleRole = AccessibleRole.CheckButton;
        AccessibleDescription = "左侧太阳为亮色，右侧月亮为暗黑。点击切换。";
        Cursor = Cursors.Hand;
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        UseVisualStyleBackColor = false;
        Size = new Size(92,38);
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer,true);
    }

    protected override void OnClick(EventArgs e) { DarkSelected = !DarkSelected; base.OnClick(e); }
    protected override void OnMouseEnter(EventArgs e) { hovering = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { hovering = false; Invalidate(); base.OnMouseLeave(e); }
    protected override bool IsInputKey(Keys keyData) =>
        (keyData & Keys.KeyCode) is Keys.Left or Keys.Right || base.IsInputKey(keyData);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode is Keys.Left or Keys.Right)
        {
            DarkSelected = e.KeyCode == Keys.Right;
            e.Handled = e.SuppressKeyPress = true;
        }
        else if (e.KeyCode == Keys.Enter)
        {
            PerformClick();
            e.Handled = e.SuppressKeyPress = true;
        }
        base.OnKeyDown(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var ancestor = Parent;
        while (ancestor is not null && ancestor.BackColor.A != 255) ancestor = ancestor.Parent;
        e.Graphics.Clear(ancestor?.BackColor ?? AppVisuals.Canvas);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var scale = DeviceDpi / 96F;
        var bounds = new RectangleF(.75F,.75F,Width-1.5F,Height-1.5F);
        if (bounds.Width <= 0 || bounds.Height <= 0) return;
        using var track = Round(bounds,bounds.Height/2);
        using var background = new SolidBrush(hovering && Enabled ? AppVisuals.ControlPressed : AppVisuals.SurfaceAlt);
        e.Graphics.FillPath(background,track);
        using var outline = new Pen(AppVisuals.Line,scale);
        e.Graphics.DrawPath(outline,track);
        var padding = 3*scale;
        var segmentWidth = (bounds.Width-2*padding)/2;
        var segment = new RectangleF(bounds.X+padding+(DarkSelected ? segmentWidth : 0),
            bounds.Y+padding,segmentWidth,bounds.Height-2*padding);
        using var thumb = Round(segment,segment.Height/2);
        using var selected = new SolidBrush(AppVisuals.AccentSoft);
        e.Graphics.FillPath(selected,thumb);
        var left = new PointF(bounds.X+padding+segmentWidth/2,bounds.Y+bounds.Height/2);
        var right = new PointF(left.X+segmentWidth,left.Y);
        DrawSun(e.Graphics,left,scale,DarkSelected ? AppVisuals.MutedInk : AppVisuals.Accent);
        DrawMoon(e.Graphics,right,scale,DarkSelected ? AppVisuals.Accent : AppVisuals.MutedInk,
            DarkSelected ? AppVisuals.AccentSoft : hovering && Enabled ? AppVisuals.ControlPressed : AppVisuals.SurfaceAlt);
        if (Focused && ShowFocusCues)
            ControlPaint.DrawFocusRectangle(e.Graphics,Rectangle.Inflate(ClientRectangle,-5,-5),AppVisuals.Ink,AppVisuals.SurfaceAlt);
    }

    private static void DrawSun(Graphics graphics, PointF center, float scale, Color color)
    {
        using var pen = new Pen(color,1.5F*scale) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        var radius = 3.2F*scale;
        graphics.DrawEllipse(pen,center.X-radius,center.Y-radius,2*radius,2*radius);
        for (var i=0;i<8;i++)
        {
            var angle = i*Math.PI/4;
            graphics.DrawLine(pen,
                center.X+(float)Math.Cos(angle)*5.7F*scale,center.Y+(float)Math.Sin(angle)*5.7F*scale,
                center.X+(float)Math.Cos(angle)*8*scale,center.Y+(float)Math.Sin(angle)*8*scale);
        }
    }

    private static void DrawMoon(Graphics graphics, PointF center, float scale, Color color, Color background)
    {
        var radius = 7.5F*scale;
        var circle = new RectangleF(center.X-radius,center.Y-radius,2*radius,2*radius);
        using var clip = new GraphicsPath();
        clip.AddEllipse(circle);
        var state = graphics.Save();
        graphics.SetClip(clip,CombineMode.Intersect);
        using var ink = new SolidBrush(color);
        using var cutout = new SolidBrush(background);
        graphics.FillEllipse(ink,circle);
        graphics.FillEllipse(cutout,circle.X+4*scale,circle.Y-3*scale,circle.Width,circle.Height);
        graphics.Restore(state);
    }

    private static GraphicsPath Round(RectangleF rect, float radius)
    {
        var path = new GraphicsPath();
        var d = Math.Max(1,Math.Min(2*radius,Math.Min(rect.Width,rect.Height)));
        path.AddArc(rect.X,rect.Y,d,d,180,90);
        path.AddArc(rect.Right-d,rect.Y,d,d,270,90);
        path.AddArc(rect.Right-d,rect.Bottom-d,d,d,0,90);
        path.AddArc(rect.X,rect.Bottom-d,d,d,90,90);
        path.CloseFigure();
        return path;
    }

    protected override AccessibleObject CreateAccessibilityInstance() => new ToggleAccessibility(this);
    private sealed class ToggleAccessibility(AppearanceToggle owner) : ControlAccessibleObject(owner)
    {
        public override AccessibleStates State => base.State | (owner.DarkSelected ? AccessibleStates.Checked : 0);
        public override string DefaultAction => "切换外观";
        public override void DoDefaultAction() => owner.PerformClick();
    }
}
