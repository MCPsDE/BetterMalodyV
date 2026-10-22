using System.Diagnostics;
using System.Drawing.Drawing2D;

namespace BetterMalodyV.Windows;

public sealed class BrandBar : UserControl
{
    public const string Watermark = "by 猫猫虫小窝(451586654)";
    public const string SponsorUrl = "https://ifdian.net/a/mcpsde";
    private readonly AppearanceToggle? appearance;
    private bool refreshing;

    public BrandBar(bool showManagerActions = true)
    {
        Dock = DockStyle.Bottom;
        Height = showManagerActions ? 58 : 40;
        BackColor = AppVisuals.Canvas;
        ForeColor = AppVisuals.Ink;
        Padding = new Padding(24,10,24,10);
        var row = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = showManagerActions ? 4 : 1, RowCount = 1, Margin = Padding.Empty };
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
        var credit = new Label
        {
            Text = Watermark, AutoSize = false, Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft, ForeColor = AppVisuals.MutedInk,
            Font = AppVisuals.UiFont(9F), UseMnemonic = false, Margin = Padding.Empty,
            AccessibleName = Watermark
        };
        row.Controls.Add(credit,0,0);
        Controls.Add(row);
        if (!showManagerActions) return;

        row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,44));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,108));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,124));
        var label = new Label { Text = "外观", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = AppVisuals.MutedInk, Font = AppVisuals.UiFont(9F), Margin = Padding.Empty };
        appearance = new AppearanceToggle
        {
            Name = "appearanceToggle", Dock = DockStyle.Fill,
            Margin = new Padding(0,0,16,0)
        };
        RefreshAppearance();
        appearance.DarkSelectedChanged += (_,_) =>
        {
            if (refreshing) return;
            try { AppAppearance.SetDarkMode(appearance.DarkSelected); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                RefreshAppearance();
                MessageBox.Show(FindForm(), "无法保存外观设置：" + ex.Message, "外观", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        };
        var sponsor = new SponsorButton { Name = "sponsorButton", Text = "赞助支持", Dock = DockStyle.Fill,
            Font = AppVisuals.UiFont(10F,FontStyle.Bold), Margin = Padding.Empty, AccessibleName = "赞助支持" };
        sponsor.Click += (_,_) =>
        {
            try { Process.Start(new ProcessStartInfo(SponsorUrl) { UseShellExecute = true }); }
            catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
            {
                MessageBox.Show(FindForm(), "无法打开赞助页面：" + ex.Message, "赞助支持", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        };
        row.Controls.Add(label,1,0);
        row.Controls.Add(appearance,2,0);
        row.Controls.Add(sponsor,3,0);
    }

    internal void RefreshAppearance()
    {
        refreshing = true;
        try { if (appearance is not null) appearance.DarkSelected = AppAppearance.IsDark; }
        finally { refreshing = false; }
    }
}

internal sealed class SponsorButton : Button
{
    private bool hovering;
    private bool pressing;
    public SponsorButton()
    {
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        Cursor = Cursors.Hand;
        UseVisualStyleBackColor = false;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer,true);
    }

    protected override void OnMouseEnter(EventArgs e) { hovering = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { hovering = pressing = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs e) { pressing = true; Invalidate(); base.OnMouseDown(e); }
    protected override void OnMouseUp(MouseEventArgs e) { pressing = false; Invalidate(); base.OnMouseUp(e); }
    protected override void OnPaint(PaintEventArgs e)
    {
        var ancestor = Parent;
        while (ancestor is not null && ancestor.BackColor.A != 255) ancestor = ancestor.Parent;
        e.Graphics.Clear(ancestor?.BackColor ?? AppVisuals.Canvas);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var rect = new RectangleF(.5F,.5F,Width-1,Height-1);
        var diameter = 8F * DeviceDpi / 96;
        using var path = new GraphicsPath();
        path.AddArc(rect.X,rect.Y,diameter,diameter,180,90);
        path.AddArc(rect.Right-diameter,rect.Y,diameter,diameter,270,90);
        path.AddArc(rect.Right-diameter,rect.Bottom-diameter,diameter,diameter,0,90);
        path.AddArc(rect.X,rect.Bottom-diameter,diameter,diameter,90,90);
        path.CloseFigure();
        using var gradient = new LinearGradientBrush(rect,Color.FromArgb(100,43,177),Color.FromArgb(24,72,143),12F);
        gradient.InterpolationColors = new ColorBlend
        {
            Colors = new[] {Color.FromArgb(100,43,177),Color.FromArgb(166,29,88),Color.FromArgb(24,72,143)},
            Positions = new[] {0F,.52F,1F}
        };
        e.Graphics.FillPath(gradient,path);
        if (hovering || pressing)
        {
            using var overlay = new SolidBrush(pressing ? Color.FromArgb(35,Color.Black) : Color.FromArgb(16,Color.White));
            e.Graphics.FillPath(overlay,path);
        }
        TextRenderer.DrawText(e.Graphics,Text,Font,ClientRectangle,Color.White,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
        if (Focused && ShowFocusCues)
            ControlPaint.DrawFocusRectangle(e.Graphics,Rectangle.Inflate(ClientRectangle,-4,-4),Color.White,Color.FromArgb(100,43,177));
    }
}
