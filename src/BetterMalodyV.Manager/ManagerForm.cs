using System.Reflection;
using BetterMalodyV.Windows;

namespace BetterMalodyV.Manager;

internal sealed class ManagerForm : Form
{
    private readonly MarkdownView document = new();
    private readonly Label status = AppVisuals.TextBlock("正在读取功能说明…", 9f);
    private readonly Button update = AppVisuals.CreateButton("检查更新");
    private readonly System.Windows.Forms.Timer watcher = new() { Interval = 1000 };
    private readonly CancellationTokenSource lifetime = new();
    private readonly string featurePath;
    private readonly bool skipUpdates;
    private string lastMarkdown = "";
    private bool checking;
    internal static string Version => typeof(ManagerForm).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0] ?? "1.0.0";
    internal static string GameRoot => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, ".."));

    public ManagerForm(bool skipUpdates = false)
    {
        this.skipUpdates = skipUpdates;
        Text = "BetterMalodyV · 管理面板"; Icon = AppVisuals.CreateAppIcon();
        AutoScaleMode = AutoScaleMode.Dpi; Font = AppVisuals.UiFont();
        ClientSize = new Size(960, 780); MinimumSize = new Size(720, 580);
        StartPosition = FormStartPosition.CenterScreen; BackColor = AppVisuals.Canvas;
        featurePath = Environment.GetEnvironmentVariable("BETTER_MALODYV_FEATURES") ?? Path.Combine(AppContext.BaseDirectory, "docs", "FEATURES.md");
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, Padding = new Padding(32, 24, 32, 16) };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize)); root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        var header = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2, Margin = new Padding(0,0,0,20) };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 76)); header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        header.Controls.Add(new PictureBox { Image = AppVisuals.CreateLogoImage(), SizeMode = PictureBoxSizeMode.Zoom, Size = new Size(58,58), Margin = new Padding(0,0,18,0) },0,0);
        var title = AppVisuals.Stack();
        AppVisuals.AddRow(title, AppVisuals.TextBlock("优化与修改", 22f, true));
        AppVisuals.AddRow(title, AppVisuals.TextBlock($"BetterMalodyV  {Version}", 10f));
        header.Controls.Add(title,1,0); root.Controls.Add(header,0,0);
        var refresh = AppVisuals.CreateButton("刷新说明"); refresh.Click += (_,_) => LoadDocument(true);
        update.Click += async (_,_) => await CheckUpdatesAsync();
        root.Controls.Add(new ButtonRow(refresh, update) { Margin = new Padding(0,0,0,20) },0,1);
        root.Controls.Add(document,0,2);
        status.Dock = DockStyle.Fill; status.Margin = new Padding(0,14,0,0); root.Controls.Add(status,0,3);
        Controls.Add(root); Controls.Add(new BrandBar());
        watcher.Tick += (_,_) => LoadDocument(false); watcher.Start();
        KeyPreview = true; KeyDown += (_,e) => { if (e.KeyCode == Keys.F5) { LoadDocument(true); e.Handled = true; } };
        AppAppearance.Initialize(this); LoadDocument(true);
        Shown += async (_,_) => { if (!skipUpdates) await CheckUpdatesAsync(); };
        FormClosed += (_,_) => { lifetime.Cancel(); watcher.Dispose(); lifetime.Dispose(); };
        document.BackColorChanged += (_,_) => { if (lastMarkdown.Length > 0) document.Render(lastMarkdown); };
    }

    internal void LoadDocument(bool force)
    {
        try
        {
            string markdown;
            if (File.Exists(featurePath)) markdown = File.ReadAllText(featurePath);
            else
            {
                using var stream = typeof(ManagerForm).Assembly.GetManifestResourceStream("BetterMalodyV.FEATURES.md")!;
                using var reader = new StreamReader(stream); markdown = reader.ReadToEnd();
            }
            if (force || markdown != lastMarkdown) { document.Render(markdown); lastMarkdown = markdown; }
            if (!checking && force) status.Text = File.Exists(featurePath) ? "说明已刷新。保存文档后会自动更新此页面。" : "正在显示随附的功能说明。";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        { if (!checking) status.Text = "说明读取失败，保留当前内容：" + ex.Message; }
    }

    private async Task CheckUpdatesAsync()
    {
        if (checking) return;
        checking = true; update.Enabled = false; status.Text = "正在检查 GitHub 更新…";
        try
        {
            var result = await UpdateService.CheckAndStartAsync(GameRoot, new Progress<string>(s => { if (!IsDisposed) status.Text = s; }), lifetime.Token);
            if (IsDisposed) return;
            status.Text = result.State == UpdateState.None ? "已完成更新检查。" : result.Message;
            if (result.State == UpdateState.Started) Close();
        }
        finally { checking = false; if (!IsDisposed) update.Enabled = true; }
    }
}
