using System.Diagnostics;
using BetterMalodyV.Shared;

namespace BetterMalodyV.Manager;

internal static class GameUpdateWorker
{
    internal static async Task RunAsync(string gameRoot,int gamePid)
    {
        var folder=Path.Combine(gameRoot,"BetterMalodyV",".updates");Directory.CreateDirectory(folder);
        var failure=Path.Combine(folder,"last-failure.txt");
        try
        {
            if(File.Exists(failure) && DateTime.UtcNow-File.GetLastWriteTimeUtc(failure)<TimeSpan.FromHours(1))return;
            using var game=Process.GetProcessById(gamePid);
            if(!GameUpdateProtocol.IsExpectedGame(gameRoot,game.MainModule?.FileName))return;
            var progress=new FileProgress(gameRoot,gamePid);
            progress.Report(new(GameUpdateStage.Checking));
            using var cancel=new CancellationTokenSource();
            game.EnableRaisingEvents=true;game.Exited+=(_,_)=>cancel.Cancel();
            var result=await UpdateService.CheckAndStartAsync(gameRoot,cancellationToken:cancel.Token,gameProcessId:gamePid,gameProgress:progress);
            if(result.State!=UpdateState.Started)progress.Report(new(result.State==UpdateState.Failed?GameUpdateStage.Failed:GameUpdateStage.None,progress.Version));
            if(result.State==UpdateState.Failed)File.WriteAllText(failure,result.Message);
        }
        catch(Exception ex) when(ex is IOException or InvalidOperationException or ArgumentException or System.ComponentModel.Win32Exception)
        {File.WriteAllText(failure,ex.Message);}
    }
    private sealed class FileProgress(string root,int pid):IProgress<GameUpdateProgress>
    {
        internal string Version="";
        public void Report(GameUpdateProgress value)
        {
            Version=value.Version;
            try {GameUpdateProtocol.WriteProgress(root,pid,value);}
            catch(Exception ex) when(ex is IOException or UnauthorizedAccessException) { }
        }
    }
}
