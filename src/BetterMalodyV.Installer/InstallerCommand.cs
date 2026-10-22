namespace BetterMalodyV.Installer;

internal static class InstallerCommand
{
    public static int Run(string[] args)
    {
        try
        {
            if (args.Length == 3 && args[0] == "--update")
                return RunUpdate(args[1], int.Parse(args[2]));
            if (args.Length != 2)
            {
                Console.Error.WriteLine(
                    "Usage: --check <game-dir> | --install <game-dir> | --uninstall <game-dir> | --verify <game-dir>");
                return 2;
            }

            var progress = Environment.GetEnvironmentVariable(
                "BETTER_MALODYV_QUIET") == "1"
                ? null
                : new Progress<string>(Console.WriteLine);
            var service = new InstallService();
            var result = args[0].ToLowerInvariant() switch
            {
                "--check" => Check(args[1]),
                "--install" => service.InstallOrRepair(args[1], progress),
                "--uninstall" => service.Uninstall(args[1], progress),
                "--verify" => service.Verify(args[1]),
                _ => OperationResult.Fail($"Unknown command: {args[0]}")
            };

            var writer = result.Success ? Console.Out : Console.Error;
            writer.WriteLine(result.Message);
            return result.Success ? 0 : 1;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 1;
        }
    }

    private static int RunUpdate(string gameRoot, int managerPid)
    {
        try
        {
            try
            {
                using var manager = System.Diagnostics.Process.GetProcessById(managerPid);
                if (!manager.WaitForExit(30000)) throw new IOException("管理面板尚未退出，请关闭后重试更新。");
            }
            catch (ArgumentException) { /* Manager already exited. */ }
            var result = new InstallService().InstallOrRepair(gameRoot);
            if (!result.Success)
                MessageBox.Show("更新未完成，原有安装已保留。请退出游戏后重试。\n" + result.Message, "BetterMalodyV 更新");
            var executable = Path.Combine(gameRoot, ProductInfo.ManagerRelativePath);
            if (File.Exists(executable))
            {
                var start = new System.Diagnostics.ProcessStartInfo(executable) { UseShellExecute = true };
                start.ArgumentList.Add("--skip-update");
                System.Diagnostics.Process.Start(start);
            }
            return result.Success ? 0 : 1;
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "BetterMalodyV 更新失败"); return 1;
        }
    }

    private static OperationResult Check(string gameRoot)
    {
        if (!GameLocator.IsGameDirectory(gameRoot, out var error)) return OperationResult.Fail(error);
        var environment = BepInExEnvironment.Inspect(gameRoot);
        return environment.Present && !environment.Compatible
            ? OperationResult.Fail(environment.Message)
            : OperationResult.Ok(environment.Message);
    }
}
