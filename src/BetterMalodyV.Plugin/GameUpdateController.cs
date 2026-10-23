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
    public void Update()
    {
        if(quitting || Environment.GetEnvironmentVariable("BETTER_MALODYV_DISABLE_UPDATES")=="1")return;
        if(Time.unscaledTime>=nextRequest)
        {
            nextRequest=Time.unscaledTime+1;
            var request=GameUpdateProtocol.RequestPath(BepInEx.Paths.GameRootPath,Environment.ProcessId);
            updatePending=File.Exists(request);
            if(updatePending && CanRestart())
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
            var start=new ProcessStartInfo(helper){UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden,WorkingDirectory=Path.GetDirectoryName(helper)!};
            start.ArgumentList.Add("--game-update");start.ArgumentList.Add(BepInEx.Paths.GameRootPath);start.ArgumentList.Add(Environment.ProcessId.ToString());
            worker=Process.Start(start);
        }
        catch(Exception ex){Plugin.Current.LogRoom("Game update check could not start: "+ex.Message);}
    }
    private static bool CanRestart()
    {
        // Let the current play session finish before requesting a normal quit.
        foreach(var component in UnityEngine.Object.FindObjectsOfType<Transform>())
        {
            var scene=component.gameObject.scene.name;
            if(scene.StartsWith("ScenePlay",StringComparison.OrdinalIgnoreCase) || scene.StartsWith("PlayMode",StringComparison.OrdinalIgnoreCase) || scene.StartsWith("SceneEdit",StringComparison.OrdinalIgnoreCase) || scene.StartsWith("EditMode",StringComparison.OrdinalIgnoreCase))return false;
        }
        var room=UnityEngine.Object.FindObjectOfType<Malody.Scene.SceneRoom>();
        if(!room)return true;
        if(RoomChartController.Current is null)return false;
        var info=RoomChartController.Current.Api.Info(room);return info is not null && info.Status==0;
    }
}
