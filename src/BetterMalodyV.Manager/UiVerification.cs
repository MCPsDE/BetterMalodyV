using BetterMalodyV.Windows;

namespace BetterMalodyV.Manager;

internal static class UiVerification
{
    public static void Run(string[] args)
    {
        var output = Path.GetFullPath(args.SkipWhile(a => a != "--ui-test").Skip(1).First());
        Directory.CreateDirectory(output);
        var checks = new List<string>();
        foreach (var dark in new[] { true, false })
        foreach (var size in new[] { new Size(960,780), new Size(704,541) })
        foreach (var scale in new[] { 1f, 1.25f, 1.5f, 2f })
        {
            AppAppearance.SetDarkMode(dark, persist: false);
            using var form = new ManagerForm(true);
            form.ShowInTaskbar = false; form.StartPosition = FormStartPosition.Manual; form.Location = new Point(-25000,-25000);
            form.ClientSize = size;
            form.Show(); Application.DoEvents();
            if (scale != 1) form.Scale(new SizeF(scale,scale));
            form.PerformLayout(); Application.DoEvents();
            Validate(form, checks);
            using var image = new Bitmap(form.Width,form.Height);
            form.DrawToBitmap(image,new Rectangle(Point.Empty,image.Size));
            image.Save(Path.Combine(output,$"manager-{(dark ? "dark" : "light")}-{size.Width}-{scale:0.##}.png"));
            form.Close();
        }
        File.WriteAllLines(Path.Combine(output,"checks.txt"),checks);
        if (checks.Any(s => s.StartsWith("FAIL"))) throw new InvalidOperationException("UI layout failed. See checks.txt.");
    }

    private static void Validate(Control root,List<string> checks)
    {
        foreach (Control child in root.Controls)
        {
            if (child is Button button && child.Visible)
            {
                var measured = TextRenderer.MeasureText(button.Text,button.Font,Size.Empty,TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);
                var fits = measured.Width + 24 * button.DeviceDpi / 96 <= button.Width && measured.Height + 4 <= button.Height;
                checks.Add($"{(fits ? "PASS" : "FAIL")} {button.Text} width={button.Width}, text={measured.Width}, dpi={button.DeviceDpi}");
            }
            Validate(child,checks);
        }
    }
}
