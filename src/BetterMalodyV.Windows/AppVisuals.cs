using System.Drawing.Drawing2D;
using System.Reflection;

namespace BetterMalodyV.Windows;

public static class AppVisuals
{
    private const string IconResourceName =
        "BetterMalodyV.Windows.Assets.mtp.ico";
    private const string LogoResourceName =
        "BetterMalodyV.Windows.Assets.mtp.png";

    private static readonly Lazy<string> UiFontFamily = new(() =>
        ResolveFontFamily(
            "Microsoft YaHei UI",
            "Segoe UI Variable Text",
            "Segoe UI"));
    private static readonly Lazy<string> MonoFontFamily = new(() =>
        ResolveFontFamily("Cascadia Mono", "Consolas", "Courier New"));

    internal static bool DarkMode { get; set; } = AppearancePreferences.LoadDarkMode();
    public static Color Ink => Palette(DarkMode).Ink;
    public static Color MutedInk => Palette(DarkMode).MutedInk;
    public static Color Canvas => Palette(DarkMode).Canvas;
    public static Color Surface => Palette(DarkMode).Surface;
    public static Color SurfaceAlt => Palette(DarkMode).SurfaceAlt;
    public static Color SurfaceRaised => Surface;
    public static Color Line => Palette(DarkMode).Line;
    public static Color Accent => Palette(DarkMode).Accent;
    public static Color AccentHover => Color.FromArgb(17,94,163);
    public static Color AccentPressed => Color.FromArgb(0,84,147);
    public static Color AccentFill => Color.FromArgb(15,108,189);
    public static Color AccentSoft => Palette(DarkMode).AccentSoft;
    public static Color Danger => Palette(DarkMode).Danger;
    public static Color DangerFill => Color.FromArgb(196,49,36);
    public static Color DangerHover => Color.FromArgb(164,38,28);
    public static Color DangerPressed => Color.FromArgb(139,31,23);
    public static Color Sidebar => Surface;
    public static Color SidebarMuted => MutedInk;
    public static Color SidebarSoft => SurfaceAlt;
    public static Color Success => Palette(DarkMode).Success;
    public static Color ControlPressed => DarkMode ? Color.FromArgb(51,51,51) : Color.FromArgb(237,237,237);

    internal static AppPalette Palette(bool dark) => dark
        ? new(Color.FromArgb(245,245,245), Color.FromArgb(184,184,184), Color.FromArgb(24,24,24),
            Color.FromArgb(32,32,32), Color.FromArgb(41,41,41), Color.FromArgb(68,68,68),
            Color.FromArgb(105,175,237), Color.FromArgb(27,52,72), Color.FromArgb(244,149,142), Color.FromArgb(126,210,126))
        : new(Color.FromArgb(36,36,36), Color.FromArgb(97,97,97), Color.FromArgb(245,245,245),
            Color.White, Color.FromArgb(250,250,250), Color.FromArgb(225,225,225),
            Color.FromArgb(15,108,189), Color.FromArgb(229,241,251), Color.FromArgb(196,49,36), Color.FromArgb(16,124,16));

    public static Font UiFont(float size = 10F, FontStyle style = FontStyle.Regular)
    {
        return new Font(UiFontFamily.Value, size, style, GraphicsUnit.Point);
    }

    public static Font MonoFont(float size = 9F, FontStyle style = FontStyle.Regular)
    {
        return new Font(MonoFontFamily.Value, size, style, GraphicsUnit.Point);
    }

    public static Icon CreateAppIcon()
    {
        try
        {
            using var stream = Assembly.GetExecutingAssembly()
                .GetManifestResourceStream(IconResourceName);
            if (stream != null)
            {
                return new Icon(stream);
            }
        }
        catch
        {
        }

        return (Icon)SystemIcons.Application.Clone();
    }

    public static Image CreateLogoImage()
    {
        try
        {
            using var stream = Assembly.GetExecutingAssembly()
                .GetManifestResourceStream(LogoResourceName);
            if (stream != null)
            {
                using var image = Image.FromStream(stream);
                return new Bitmap(image);
            }
        }
        catch
        {
        }

        var fallback = new Bitmap(96, 96);
        using var graphics = Graphics.FromImage(fallback);
        graphics.Clear(Color.Transparent);
        using var brush = new SolidBrush(Accent);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.FillEllipse(brush, 12, 12, 72, 72);
        return fallback;
    }

    public static Button CreateButton(
        string text,
        bool primary = false,
        bool danger = false)
    {
        var button = new ActionButton
        {
            Primary = primary,
            Danger = danger,
            AutoSize = false,
            Height = 36,
            Text = text,
            Font = UiFont(10F),
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand,
            UseVisualStyleBackColor = false,
            TextAlign = ContentAlignment.MiddleCenter,
            BackColor = primary
                ? AccentFill
                : danger
                    ? DangerFill
                    : Surface,
            ForeColor = primary || danger ? Color.White : Ink
        };
        button.AccessibleName = text.Replace("&", "");
        button.FlatAppearance.BorderSize = primary || danger ? 0 : 1;
        button.FlatAppearance.BorderColor = Line;
        button.FlatAppearance.MouseOverBackColor = primary
            ? AccentHover
            : danger
                ? DangerHover
                : SurfaceAlt;
        button.FlatAppearance.MouseDownBackColor = primary
            ? AccentPressed
            : danger
                ? DangerPressed
                : ControlPressed;
        return button;
    }

    public static TableLayoutPanel Stack(int padding = 0)
    {
        var table = new TableLayoutPanel
        {
            Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, ColumnCount = 1,
            Padding = new Padding(padding), BackColor = Color.Transparent,
            Margin = Padding.Empty
        };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        return table;
    }

    public static Label TextBlock(string text, float size = 10F, bool bold = false) => new()
    {
        Text = text, AutoSize = true, Dock = DockStyle.Top,
        Font = UiFont(size, bold ? FontStyle.Bold : FontStyle.Regular),
        ForeColor = bold ? Ink : MutedInk, Margin = new Padding(0, 0, 0, 12),
        UseMnemonic = false
    };

    public static SingleLineLabel SingleLineText(string text, float size = 10F, bool bold = false) => new()
    {
        Text = text, Dock = DockStyle.Top, Font = UiFont(size, bold ? FontStyle.Bold : FontStyle.Regular),
        ForeColor = bold ? Ink : MutedInk, Margin = new Padding(0, 0, 0, 12)
    };

    public static void AddRow(TableLayoutPanel table, Control control)
    {
        var row = table.RowCount++;
        table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        table.Controls.Add(control, 0, row);
    }

    public static Label CreateLabel(
        string text,
        float size,
        Color color,
        FontStyle style = FontStyle.Regular)
    {
        return new Label
        {
            AutoSize = true,
            Text = text,
            ForeColor = color,
            Font = UiFont(size, style)
        };
    }

    private static string ResolveFontFamily(params string[] candidates)
    {
        foreach (var candidate in candidates)
        {
            try
            {
                using var font = new Font(candidate, 9F);
                if (string.Equals(
                        font.FontFamily.Name,
                        candidate,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return candidate;
                }
            }
            catch
            {
            }
        }

        return FontFamily.GenericSansSerif.Name;
    }
}

internal sealed record AppPalette(Color Ink, Color MutedInk, Color Canvas, Color Surface,
    Color SurfaceAlt, Color Line, Color Accent, Color AccentSoft, Color Danger, Color Success);

public sealed class Separator : Control
{
    public Separator()
    {
        Height = 1;
        Dock = DockStyle.Top;
        BackColor = AppVisuals.Line;
        SetStyle(ControlStyles.Selectable, false);
    }
}
