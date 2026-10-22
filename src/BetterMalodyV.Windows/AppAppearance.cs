using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace BetterMalodyV.Windows;

public static class AppAppearance
{
    private sealed class FormState { public bool Dark { get; set; } }
    private static readonly ConditionalWeakTable<Form, FormState> States = new();
    public static bool IsDark => AppVisuals.DarkMode;

    /// <summary>Installer process override; never changes the saved manager preference.</summary>
    public static void UseLightAppearance() => SetDarkMode(false,persist: false);

    public static void Initialize(Form form)
    {
        if (!States.TryGetValue(form, out _))
            States.Add(form, new FormState { Dark = IsDark });
        Apply(form, AppVisuals.Palette(IsDark), AppVisuals.Palette(IsDark));
        form.HandleCreated += (_, _) => WindowsBackdrop.TryApplyMica(form);
        WindowsBackdrop.TryApplyMica(form);
    }

    public static void SetDarkMode(bool dark, bool persist = true)
    {
        // Persist before changing the UI so a failed write leaves the selector honest.
        if (persist) AppearancePreferences.SaveDarkMode(dark);
        AppVisuals.DarkMode = dark;
        foreach (Form form in Application.OpenForms)
        {
            if (!States.TryGetValue(form, out var state)) continue;
            form.SuspendLayout();
            Apply(form, AppVisuals.Palette(state.Dark), AppVisuals.Palette(dark));
            state.Dark = dark;
            WindowsBackdrop.TryApplyMica(form);
            form.ResumeLayout(true);
            form.Invalidate(true);
        }
    }

    private static void Apply(Control control, AppPalette from, AppPalette to)
    {
        control.BackColor = Translate(control.BackColor, from, to, false);
        control.ForeColor = Translate(control.ForeColor, from, to, true);
        if (control is ActionButton button)
        {
            if (button.Primary || button.Danger)
            {
                button.BackColor = button.Primary ? AppVisuals.AccentFill : AppVisuals.DangerFill;
                button.ForeColor = Color.White;
            }
            button.FlatAppearance.BorderColor = to.Line;
        }
        if (control is CheckBox checkBox) checkBox.UseVisualStyleBackColor = false;
        if (control is BrandBar bar) bar.RefreshAppearance();
        if (control is TextBoxBase or ListBox or ComboBox or ProgressBar or ScrollableControl or TreeView)
        {
            if (control.IsHandleCreated) ApplyNativeTheme(control);
            else control.HandleCreated += (_, _) => ApplyNativeTheme(control);
        }
        foreach (Control child in control.Controls) Apply(child, from, to);
    }

    private static Color Translate(Color color, AppPalette from, AppPalette to, bool foreground)
    {
        if (color.A == 0) return color;
        if (color.ToArgb() == from.Ink.ToArgb()) return to.Ink;
        if (color.ToArgb() == from.MutedInk.ToArgb()) return to.MutedInk;
        // NumericUpDown propagates its new text color to its native child before
        // recursion reaches that child. Do not reinterpret that text as a
        // background token (dark ink and light canvas share #F5F5F5).
        if (!foreground)
        {
            if (color.ToArgb() == from.Canvas.ToArgb()) return to.Canvas;
            if (color.ToArgb() == from.Surface.ToArgb()) return to.Surface;
            if (color.ToArgb() == from.SurfaceAlt.ToArgb()) return to.SurfaceAlt;
            if (color.ToArgb() == from.Line.ToArgb()) return to.Line;
        }
        if (color.ToArgb() == from.Accent.ToArgb()) return to.Accent;
        if (color.ToArgb() == from.AccentSoft.ToArgb()) return to.AccentSoft;
        if (color.ToArgb() == from.Danger.ToArgb()) return to.Danger;
        if (color.ToArgb() == from.Success.ToArgb()) return to.Success;
        if (foreground && (color == SystemColors.ControlText || color == SystemColors.WindowText)) return to.Ink;
        if (!foreground && (color == SystemColors.Control || color == SystemColors.Window)) return to.Surface;
        return color;
    }

    private static void ApplyNativeTheme(Control control)
    {
        if (!OperatingSystem.IsWindows()) return;
        _ = SetWindowTheme(control.Handle, IsDark ? "DarkMode_Explorer" : "Explorer", null);
    }

    [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
    private static extern int SetWindowTheme(IntPtr hwnd, string subAppName, string? subIdList);
}
