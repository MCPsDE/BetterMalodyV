using System.Diagnostics;
using BetterMalodyV.Windows;

namespace BetterMalodyV.Installer;

internal sealed class InstallerForm : Form
{
    private readonly TextBox pathBox = new();
    private readonly Label pathState;
    private readonly Label environmentState;
    private readonly Label operationState;
    private readonly TextBox statusBox = new();
    private readonly ProgressBar progressBar = new();
    private readonly Button browseButton;
    private readonly Button installButton;
    private readonly Button uninstallButton;
    private readonly Button managerButton;
    private readonly InstallService service = new();
    private readonly System.Windows.Forms.Timer pathTimer = new() { Interval = 350 };
    private bool busy;

    public InstallerForm()
    {
        AppAppearance.UseLightAppearance();
        pathState = AppVisuals.TextBlock("");
        environmentState = AppVisuals.TextBlock("");
        operationState = AppVisuals.TextBlock("选择游戏目录后即可安装。");
        browseButton = AppVisuals.CreateButton("浏览…");
        installButton = AppVisuals.CreateButton("安装 BetterMalodyV",primary: true);
        uninstallButton = AppVisuals.CreateButton("卸载插件");
        managerButton = AppVisuals.CreateButton("打开管理面板");
        Text = "BetterMalodyV · 安装";
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(880, 720);
        MinimumSize = new Size(760, 660);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = AppVisuals.Canvas;
        Font = AppVisuals.UiFont();
        Icon = AppVisuals.CreateAppIcon();
        Shown += (_, _) => WindowsBackdrop.TryApplyMica(this);
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3,
            Padding = new Padding(36, 28, 36, 24)
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        var header = AppVisuals.Stack();
        AppVisuals.AddRow(header, AppVisuals.TextBlock("安装 BetterMalodyV", 22F, true));
        AppVisuals.AddRow(header, AppVisuals.TextBlock("为 Malody V 安装 BetterMalodyV，之后照常启动游戏。"));
        var version = AppVisuals.TextBlock($"版本 {ProductInfo.Version}  ·  Windows x64  ·  离线安装", 9F);
        version.Margin = new Padding(0, 0, 0, 24);
        AppVisuals.AddRow(header, version);
        layout.Controls.Add(header, 0, 0);
        var content = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
        var stack = AppVisuals.Stack();
        AppVisuals.AddRow(stack, AppVisuals.TextBlock("游戏目录", 11F, true));
        var pathRow = new TableLayoutPanel { Dock = DockStyle.Top, Height = 48, ColumnCount = 2, Margin = new Padding(0,0,0,6) };
        pathRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        pathRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
        pathBox.Dock = DockStyle.Fill;
        pathBox.Margin = new Padding(0, 6, 12, 0);
        pathBox.AccessibleName = "Malody V 游戏目录";
        pathBox.TextChanged += (_, _) => { installButton.Enabled = false; pathTimer.Stop(); pathTimer.Start(); };
        browseButton.Dock = DockStyle.Fill;
        browseButton.Margin = Padding.Empty;
        browseButton.Click += (_, _) => Browse();
        pathRow.Controls.Add(pathBox, 0, 0);
        pathRow.Controls.Add(browseButton, 1, 0);
        AppVisuals.AddRow(stack, pathRow);
        AppVisuals.AddRow(stack, pathState);
        var environment = AppVisuals.Stack(20);
        environment.BackColor = AppVisuals.Surface;
        environment.Margin = new Padding(0, 8, 0, 24);
        AppVisuals.AddRow(environment, AppVisuals.TextBlock("运行环境", 11F, true));
        environmentState.Margin = Padding.Empty;
        AppVisuals.AddRow(environment, environmentState);
        AppVisuals.AddRow(stack, environment);
        AppVisuals.AddRow(stack, AppVisuals.TextBlock("安装记录", 11F, true));
        statusBox.Dock = DockStyle.Top;
        statusBox.Height = 100;
        statusBox.Multiline = statusBox.ReadOnly = true;
        statusBox.ScrollBars = ScrollBars.Vertical;
        statusBox.BorderStyle = BorderStyle.None;
        statusBox.BackColor = AppVisuals.Surface;
        statusBox.ForeColor = AppVisuals.MutedInk;
        statusBox.Font = AppVisuals.MonoFont(9F);
        statusBox.AccessibleName = "安装详细记录";
        statusBox.Text = "准备就绪。安装过程中可在这里查看详细进度。";
        AppVisuals.AddRow(stack, statusBox);
        content.Controls.Add(stack);
        layout.Controls.Add(content, 0, 1);
        var footer = AppVisuals.Stack();
        progressBar.Dock = DockStyle.Top;
        progressBar.Height = 4;
        progressBar.Margin = new Padding(0, 16, 0, 16);
        AppVisuals.AddRow(footer, progressBar);
        AppVisuals.AddRow(footer, operationState);
        var actions = new ButtonRow(installButton, managerButton, uninstallButton);
        AppVisuals.AddRow(footer, actions);
        layout.Controls.Add(footer, 0, 2);
        Controls.Add(layout);
        Controls.Add(new BrandBar(showManagerActions: false));
        installButton.Click += async (_, _) => await RunOperationAsync(false);
        uninstallButton.Click += async (_, _) =>
        {
            if (MessageBox.Show(this, "卸载 BetterMalodyV？你修改过的文件会保留。", "卸载插件",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                await RunOperationAsync(true);
        };
        managerButton.Click += (_, _) =>
        {
            try { Process.Start(new ProcessStartInfo(Path.Combine(pathBox.Text, "BetterMalodyV", "BetterMalodyV.Manager.exe")) { UseShellExecute = true }); }
            catch (Exception ex) { operationState.Text = "无法打开管理器：" + ex.Message; }
        };
        pathTimer.Tick += (_, _) => { pathTimer.Stop(); UpdatePathState(); };
        pathBox.Text = GameLocator.FindGameDirectory() ?? "";
        UpdatePathState();
        FormClosing += (_, e) => { if (busy) e.Cancel = true; };
        FormClosed += (_, _) => pathTimer.Dispose();
        AcceptButton = installButton;
        AppAppearance.Initialize(this);
    }

    private void Browse()
    {
        using var dialog = new FolderBrowserDialog { Description = "选择包含 Malody V.exe 的游戏目录", UseDescriptionForTitle = true,
            SelectedPath = Directory.Exists(pathBox.Text) ? pathBox.Text : "" };
        if (dialog.ShowDialog(this) == DialogResult.OK) pathBox.Text = dialog.SelectedPath;
    }

    private void UpdatePathState()
    {
        var valid = GameLocator.IsGameDirectory(pathBox.Text, out var error);
        pathState.Text = valid ? "已找到 Malody V" : (string.IsNullOrWhiteSpace(pathBox.Text) ? "未自动找到游戏，请浏览选择安装目录。" : error);
        pathState.ForeColor = valid ? AppVisuals.Accent : string.IsNullOrWhiteSpace(pathBox.Text) ? AppVisuals.MutedInk : AppVisuals.Danger;
        var environment = valid ? BepInExEnvironment.Inspect(pathBox.Text) : null;
        environmentState.Text = environment?.Message ?? "选择有效目录后，将自动检查 BepInEx。";
        var owned = valid && File.Exists(Path.Combine(pathBox.Text, ProductInfo.ManifestRelativePath));
        installButton.Text = owned ? "修复 BetterMalodyV" : "安装 BetterMalodyV";
        installButton.Enabled = !busy && valid && (environment?.Present != true || environment.Compatible || owned);
        uninstallButton.Enabled = !busy && owned;
        managerButton.Enabled = !busy && valid && File.Exists(Path.Combine(pathBox.Text, "BetterMalodyV", "BetterMalodyV.Manager.exe"));
    }

    private async Task RunOperationAsync(bool uninstall)
    {
        var gameRoot = pathBox.Text;
        var progress = new Progress<string>(message =>
        {
            if (statusBox.TextLength > 160_000) statusBox.Text = statusBox.Text[^80_000..];
            statusBox.AppendText(message + Environment.NewLine);
        });
        busy = true;
        pathBox.Enabled = browseButton.Enabled = installButton.Enabled = uninstallButton.Enabled = managerButton.Enabled = false;
        progressBar.Style = ProgressBarStyle.Marquee;
        statusBox.Clear();
        operationState.Text = uninstall ? "正在卸载…" : "正在安装，请稍候…";
        try
        {
            var result = await Task.Run(() => uninstall ? service.Uninstall(gameRoot, progress) : service.InstallOrRepair(gameRoot, progress));
            operationState.Text = result.Success
                ? uninstall ? "已卸载。" : "安装完成。现在可以打开管理面板，或照常启动游戏。"
                : "操作未完成：" + result.Message;
            operationState.ForeColor = result.Success ? AppVisuals.Accent : AppVisuals.Danger;
            statusBox.AppendText(result.Message + Environment.NewLine);
        }
        catch (Exception exception)
        {
            operationState.Text = "操作失败：" + exception.Message;
            operationState.ForeColor = AppVisuals.Danger;
        }
        finally
        {
            busy = false;
            pathBox.Enabled = browseButton.Enabled = true;
            progressBar.Style = ProgressBarStyle.Continuous;
            UpdatePathState();
        }
    }
}
