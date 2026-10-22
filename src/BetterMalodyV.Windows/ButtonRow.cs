namespace BetterMalodyV.Windows;

/// <summary>Fits full command labels horizontally, stacking when space is short.</summary>
public sealed class ButtonRow : Panel
{
    private readonly Button[] buttons;
    private bool arranging;
    public ButtonRow(params Button[] buttons)
    {
        this.buttons = buttons;
        // Height follows the available width in OnLayout. AutoSize would let a
        // TableLayoutPanel cache our initial narrow/stacked preferred height.
        AutoSize = false; Dock = DockStyle.Top; Margin = Padding.Empty;
        foreach (var button in buttons)
        {
            button.Dock = DockStyle.None; button.Margin = Padding.Empty;
            Controls.Add(button);
            button.TextChanged += (_, _) => PerformLayout(); button.FontChanged += (_, _) => PerformLayout();
        }
    }
    private int Gap => (int)Math.Ceiling(8 * DeviceDpi / 96f);
    private Size[] Sizes => buttons.Select(b => b.GetPreferredSize(Size.Empty)).ToArray();
    public override Size GetPreferredSize(Size proposedSize)
    {
        if (buttons == null || buttons.Length == 0) return Size.Empty;
        var sizes = Sizes; var required = sizes.Sum(s => s.Width) + Gap * (sizes.Length - 1);
        var available = proposedSize.Width > 0 ? proposedSize.Width - Padding.Horizontal : ClientSize.Width - Padding.Horizontal;
        var height = Math.Max((int)Math.Ceiling(40 * DeviceDpi / 96f), sizes.Max(s => s.Height));
        var stacked = available < required;
        return new Size((stacked ? sizes.Max(s => s.Width) : required) + Padding.Horizontal,
            (stacked ? sizes.Length * height + Gap * (sizes.Length - 1) : height) + Padding.Vertical);
    }
    protected override void OnLayout(LayoutEventArgs e)
    {
        if (arranging || buttons == null || buttons.Length == 0) return;
        arranging = true;
        try
        {
            var sizes = Sizes; var available = Math.Max(1, ClientSize.Width - Padding.Horizontal);
            var required = sizes.Sum(s => s.Width) + Gap * (sizes.Length - 1);
            var stacked = available < required;
            var height = Math.Max((int)Math.Ceiling(40 * DeviceDpi / 96f), sizes.Max(s => s.Height));
            var x = Padding.Left; var y = Padding.Top;
            for (var i = 0; i < buttons.Length; i++)
            {
                var width = stacked ? available : sizes[i].Width + (available - required) / buttons.Length;
                if (!stacked && i == buttons.Length - 1) width = ClientSize.Width - Padding.Right - x;
                buttons[i].SetBounds(x, y, width, height);
                if (stacked) y += height + Gap; else x += width + Gap;
            }
            var desiredHeight = (stacked ? buttons.Length * height + Gap * (buttons.Length - 1) : height) + Padding.Vertical;
            if (Height != desiredHeight) Height = desiredHeight;
        }
        finally { arranging = false; }
        base.OnLayout(e);
    }
}
