namespace BetterMalodyV.Installer;

internal static class InstallerCommand
{
    public static int Run(string[] args)
    {
        try
        {
            if(args.Length>=2 && args[0] is "--update" or "--update-game")
            {
                using var gate=new Mutex(false,BetterMalodyV.Shared.GameUpdateProtocol.GateName(args[1]));
                bool entered;
                try{entered=gate.WaitOne(0);}catch(AbandonedMutexException){entered=true;}
                if(!entered)return 0;
                try{return RunUpdateCommand(args);}finally{gate.ReleaseMutex();}
            }
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
    private static int RunUpdateCommand(string[] args)=>args[0]=="--update-game" && args.Length==4
        ?RunGameUpdate(args[1],int.Parse(args[2]),int.Parse(args[3]))
        :args[0]=="--update" && args.Length==3?RunUpdate(args[1],int.Parse(args[2])):2;

    private static int RunGameUpdate(string gameRoot,int workerPid,int gamePid)
    {
        string? request=null;
        var gameStopped=false;
        try
        {
            using(var game=System.Diagnostics.Process.GetProcessById(gamePid))
            {
                if(!BetterMalodyV.Shared.GameUpdateProtocol.IsExpectedGame(gameRoot,game.MainModule?.FileName))throw new IOException("游戏更新进程路径不匹配。");
                request=BetterMalodyV.Shared.GameUpdateProtocol.RequestPath(gameRoot,gamePid);
                Directory.CreateDirectory(Path.GetDirectoryName(request)!);File.WriteAllText(request,"verified");
                // Gameplay may be active: the plugin waits until it is back in a menu.
                // This helper remains outside both game and manager file locks.
                game.WaitForExit();
                gameStopped=true;
            }
            try {using var worker=System.Diagnostics.Process.GetProcessById(workerPid);if(!worker.WaitForExit(30000))throw new IOException("更新助手尚未退出。");}
            catch(ArgumentException){}
            var manager=Path.GetFullPath(Path.Combine(gameRoot,ProductInfo.ManagerRelativePath));
            foreach(var process in System.Diagnostics.Process.GetProcessesByName("BetterMalodyV.Manager"))
                using(process)
                {
                    try
                    {
                        if(string.Equals(process.MainModule?.FileName,manager,StringComparison.OrdinalIgnoreCase))
                        {process.CloseMainWindow();if(!process.WaitForExit(30000))throw new IOException("请关闭管理面板后更新。");}
                    }
                    catch(System.ComponentModel.Win32Exception){}
                }
            var result=new InstallService().InstallOrRepair(gameRoot);
            if(!result.Success)
            {
                File.WriteAllText(Path.Combine(gameRoot,"BetterMalodyV",".updates","last-failure.txt"),result.Message);
                MessageBox.Show("更新未完成，将重新启动保留的游戏版本。\n"+result.Message,"BetterMalodyV");
            }
            else
            {
                var failure=Path.Combine(gameRoot,"BetterMalodyV",".updates","last-failure.txt");
                if(File.Exists(failure))File.Delete(failure);
            }
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(Path.Combine(gameRoot,"Malody V.exe")){UseShellExecute=true,WorkingDirectory=gameRoot});
            gameStopped=false;
            return result.Success?0:1;
        }
        catch(Exception ex)
        {
            MessageBox.Show(ex.Message,"BetterMalodyV 自动更新");
            if(gameStopped)System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(Path.Combine(gameRoot,"Malody V.exe")){UseShellExecute=true,WorkingDirectory=gameRoot});
            return 1;
        }
        finally{if(request is not null && File.Exists(request))File.Delete(request);}
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
