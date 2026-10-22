using BetterMalodyV.Windows;
namespace BetterMalodyV.Installer;
internal static class UiVerification
{
    public static void Run(string path)
    {
        Directory.CreateDirectory(path);
        var checks = new List<string>();
        foreach (var compact in new[] { false,true })
        foreach (var scale in new[] { 1f,1.25f,1.5f,2f })
        {
            using var form = new InstallerForm();
            form.ShowInTaskbar=false; form.StartPosition=FormStartPosition.Manual; form.Location=new Point(-25000,-25000);
            if(compact) form.Size=form.MinimumSize;
            form.Show(); Application.DoEvents();
            if(scale != 1) form.Scale(new SizeF(scale,scale));
            form.PerformLayout(); Application.DoEvents();
            Inspect(form,checks);
            using var image=new Bitmap(form.Width,form.Height); form.DrawToBitmap(image,new Rectangle(Point.Empty,image.Size));
            image.Save(Path.Combine(path,$"installer-{(compact ? "minimum" : "normal")}-{scale:0.##}.png"));
            form.Close();
        }
        File.WriteAllLines(Path.Combine(path,"installer-checks.txt"),checks);
        if(checks.Any(s=>s.StartsWith("FAIL"))) throw new Exception("Installer label fit failed");
    }
    private static void Inspect(Control root,List<string> checks)
    {
        foreach(Control child in root.Controls)
        {
            if(child is Button b && b.Visible)
            {
                var text=TextRenderer.MeasureText(b.Text,b.Font,Size.Empty,TextFormatFlags.SingleLine|TextFormatFlags.NoPadding);
                checks.Add($"{(text.Width + 24*b.DeviceDpi/96 <= b.Width ? "PASS" : "FAIL")} {b.Text} width={b.Width} text={text.Width}");
            }
            Inspect(child,checks);
        }
    }
}
