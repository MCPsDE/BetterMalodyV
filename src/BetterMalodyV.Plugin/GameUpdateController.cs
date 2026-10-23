using System.Diagnostics;
using BetterMalodyV.Shared;
using UnityEngine;

namespace BetterMalodyV.Plugin;

public sealed class GameUpdateController : MonoBehaviour
{
    public GameUpdateController(IntPtr pointer):base(pointer) { }
    private float nextCheck=10;
    private float nextRequest;
    private Process? worker;
    private bool quitting;
    private bool updatePending;
    private readonly GameUpdateDialog dialog=new();
    private GameUpdateProgress? progress;
    private float restartAt=float.PositiveInfinity;
    public void Start()
    {
        // A reused OS process ID must not inherit a prior game's update state.
        try
        {
            foreach(var path in new[]{GameUpdateProtocol.ProgressPath(BepInEx.Paths.GameRootPath,Environment.ProcessId),GameUpdateProtocol.RequestPath(BepInEx.Paths.GameRootPath,Environment.ProcessId)})
                if(File.Exists(path))File.Delete(path);
        }
        catch(Exception ex) when(ex is IOException or UnauthorizedAccessException){Plugin.Current.LogRoom("Cannot clear previous game update state: "+ex.Message);}
    }
    public void Update()
    {
        if(quitting || Environment.GetEnvironmentVariable("BETTER_MALODYV_DISABLE_UPDATES")=="1")return;
        if(Time.unscaledTime>=nextRequest)
        {
            nextRequest=Time.unscaledTime+.2f;
            var request=GameUpdateProtocol.RequestPath(BepInEx.Paths.GameRootPath,Environment.ProcessId);
            updatePending=File.Exists(request);
            progress=GameUpdateProtocol.ReadProgress(BepInEx.Paths.GameRootPath,Environment.ProcessId)??progress;
            var canRestart=!(updatePending || progress?.Visible==true) || CanRestart();
            if(updatePending)
            {
                progress=new(GameUpdateStage.Ready,progress?.Version??"");
                if(!canRestart)restartAt=float.PositiveInfinity;
            }
            try {dialog.Render(progress,canRestart,updatePending && canRestart?float.IsPositiveInfinity(restartAt)?3:Math.Max(0,restartAt-Time.unscaledTime):0);}
            catch(Exception ex){Plugin.Current.LogRoom("Update dialog unavailable: "+ex.Message);}
            if(updatePending && canRestart && dialog.ReadyPresented && float.IsPositiveInfinity(restartAt))restartAt=Time.unscaledTime+3;
            if(updatePending && canRestart && Time.unscaledTime>=restartAt)
            {
                quitting=true;File.Delete(request);
                Plugin.Current.LogRoom("Verified update ready; gracefully quitting for installation and automatic game restart.");
                Application.Quit();return;
            }
        }
        if(updatePending)return;
        if(Time.unscaledTime<nextCheck)return;
        nextCheck=Time.unscaledTime+30*60;
        try
        {
            if(worker is not null && !worker.HasExited)return;
            worker?.Dispose();worker=null;
            var helper=Path.Combine(BepInEx.Paths.GameRootPath,"BetterMalodyV","BetterMalodyV.Manager.exe");
            if(!File.Exists(helper))return;
            progress=null;restartAt=float.PositiveInfinity;dialog.Render(null,true);
            var progressPath=GameUpdateProtocol.ProgressPath(BepInEx.Paths.GameRootPath,Environment.ProcessId);
            if(File.Exists(progressPath))File.Delete(progressPath);
            var start=new ProcessStartInfo(helper){UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden,WorkingDirectory=Path.GetDirectoryName(helper)!};
            start.ArgumentList.Add("--game-update");start.ArgumentList.Add(BepInEx.Paths.GameRootPath);start.ArgumentList.Add(Environment.ProcessId.ToString());
            worker=Process.Start(start);
        }
        catch(Exception ex){Plugin.Current.LogRoom("Game update check could not start: "+ex.Message);}
    }
    internal static bool CanRestart()
    {
        // Let the current play session finish before requesting a normal quit.
        for(var i=0;i<UnityEngine.SceneManagement.SceneManager.sceneCount;i++)
        {
            var scene=UnityEngine.SceneManagement.SceneManager.GetSceneAt(i);
            var name=scene.name;
            if(name.StartsWith("ScenePlay",StringComparison.OrdinalIgnoreCase) || name.StartsWith("PlayMode",StringComparison.OrdinalIgnoreCase) || name.StartsWith("SceneEdit",StringComparison.OrdinalIgnoreCase) || name.StartsWith("EditMode",StringComparison.OrdinalIgnoreCase))
                foreach(var root in scene.GetRootGameObjects())if(root.activeInHierarchy)return false;
        }
        var room=UnityEngine.Object.FindObjectOfType<Malody.Scene.SceneRoom>();
        if(!room)return true;
        if(RoomChartController.Current is null)return false;
        var info=RoomChartController.Current.Api.Info(room);return info is not null && info.Status==0;
    }
}
