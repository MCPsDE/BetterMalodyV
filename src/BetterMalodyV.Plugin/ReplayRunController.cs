using System.Reflection;
using HarmonyLib;
using Malody.DB;
using Malody.Play;
using Malody.Scene;
using Malody.Scene.Panel;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

namespace BetterMalodyV.Plugin;

public sealed class ReplayRunController : MonoBehaviour
{
    public ReplayRunController(IntPtr pointer):base(pointer) { }
    internal static ReplayRunController? Current;
    internal ReplayGameApi Api=null!;
    private Run? run;
    private float nextScan;
    private float nextAccountCheck;
    private string? failure;
    private sealed class Run(object info,ReplayFile replay,ReplayFile inputs,uint user)
    {
        internal readonly object Info=info;
        internal readonly ReplayFile Replay=replay,Inputs=inputs;
        internal readonly uint User=user;
        internal ChartPlayer? Player;
        internal PlayInput? Input;
        internal object? Helper,Control;
        internal int Seek=int.MinValue;
        internal bool SeenPlay;
        internal readonly float Started=Time.unscaledTime;
    }
    [Il2CppInterop.Runtime.Attributes.HideFromIl2Cpp]
    internal void Initialize(Harmony harmony)
    {
        Api=new();
        var installed=new List<MethodInfo>();
        HarmonyMethod Hook(string name)=>new(typeof(ReplayRunController),name);
        void Patch(MethodInfo method,string name,bool postfix=false)
        {installed.Add(method);harmony.Patch(method,prefix:postfix?null:Hook(name),postfix:postfix?Hook(name):null);}
        try
        {
            Patch(typeof(PanelScoreDetail).GetMethod("Show",new[]{typeof(ScoreRecord)})!,nameof(LocalShown),true);
            Patch(typeof(ChartPlayer).GetMethod("Load")!,nameof(Loading));
            Patch(Api.InputTick,nameof(TickBefore));Patch(Api.InputReset,nameof(ResetAfter),true);Patch(Api.ResetRandom,nameof(RandomAfter),true);
            foreach(var method in Api.PhysicalInputs)Patch(method,nameof(PhysicalBefore));
            Current=this;
        }
        catch {foreach(var method in installed)harmony.Unpatch(method,HarmonyPatchType.All,harmony.Id);Api=null!;throw;}
        Plugin.Current.LogRoom("Replay reenactment uses normal play input, judgement, result and submission.");
    }
    public void Update()
    {
        if(Api is null)return;
        if(failure is not null)
        {
            var message=failure;failure=null;run=null;
            ReplayGameApi.Navigate("Song");NativeUi.Message(message);return;
        }
        if(run is not null)
        {
            if(Time.unscaledTime>=nextAccountCheck)
            {nextAccountCheck=Time.unscaledTime+.5f;if(Api.UserId!=run.User){failure="登录账号已改变，重演已中止。";return;}}
            var scene=SceneManager.GetActiveScene().name;
            if(scene=="ScenePlay")run.SeenPlay=true;
            else if(run.SeenPlay || Time.unscaledTime-run.Started>45)run=null;
        }
        if(Time.unscaledTime<nextScan)return;nextScan=Time.unscaledTime+.5f;
        foreach(var panel in UnityEngine.Object.FindObjectsOfType<PanelScoreDetail>())
            try {Attach(panel);}catch(Exception ex){Plugin.Current.LogRoom("Replay action unavailable: "+ex.GetBaseException().Message);}
    }
    private static void LocalShown(PanelScoreDetail __instance)
    {try {Current?.Attach(__instance);}catch(Exception ex){Plugin.Current.LogRoom("Replay action: "+ex.GetBaseException().Message);}}
    [Il2CppInterop.Runtime.Attributes.HideFromIl2Cpp]
    internal Button? Attach(PanelScoreDetail panel)
    {
        const string name="BetterMalodyV.ReplayRun";
        var path=Api.LocalPath(panel);
        if(string.IsNullOrEmpty(path) || !panel.areaRep.activeSelf)return null;
        var existing=panel.areaRep.transform.Find(name);if(existing)return existing.GetComponent<Button>();
        var source=NativeUi.PrimaryText(panel.areaRep.transform);
        if(!source)return null;
        var native=panel.areaRep.GetComponentsInChildren<RectTransform>().Where(r=>r.parent==panel.areaRep.transform && r.rect.width>150 && r.rect.height>32 && r.GetComponentInChildren<TextMeshProUGUI>()).ToArray();
        if(native.Length==0)return null;
        var label=source!.font.HasCharacter('重',true,true)?"重演并上传":"Replay & upload";
        var button=NativeUi.Action(panel.areaRep.transform,source!,label,name);
        var rect=button.GetComponent<RectTransform>();
        rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(.5f,.5f);
        NativeUi.AlignActionRow(panel.areaRep.GetComponent<RectTransform>(),native,button);
        button.onClick.AddListener((UnityAction)(()=>
        {
            if(run is not null)return;
            try {Launch(Api.LocalPath(panel)??throw new IOException("找不到本地回放文件。"));}
            catch(Exception ex){Plugin.Current.LogRoom("Replay launch stopped: "+ex.GetBaseException().Message);NativeUi.Message(ex.GetBaseException().Message);}
        }));
        return button;
    }
    [Il2CppInterop.Runtime.Attributes.HideFromIl2Cpp]
    internal void Launch(string file)
    {
        if(run is not null)throw new IOException("已有重演正在运行。");
        var replay=Api.Read(file);var info=Api.Prepare(replay);var inputs=ReplayGameApi.InputsOnly(replay);
        run=new(info,replay,inputs,Api.UserId);Api.Use(info);
        try {ReplayGameApi.Navigate("Play");}
        catch {run=null;throw;}
    }
    private static void Loading(ChartPlayer __instance,object __0)
    {
        var owner=Current;var active=owner?.run;if(active is null || !ReplayGameApi.Same(active.Info,__0))return;
        owner!.Api.KeepSeed(__0,active.Replay.Seed);active.Player=__instance;active.Input=__instance.playInput;
        active.Helper=null;active.Control=null;active.Seek=int.MinValue;
    }
    private static bool Own(PlayInput input)
    {
        var active=Current?.run;if(active is null)return false;
        if(active.Input is null && active.Player is not null)active.Input=active.Player.playInput;
        return ReplayGameApi.Same(active.Input,input);
    }
    private static bool PhysicalBefore(PlayInput __instance)=>!Own(__instance);
    private static void RandomAfter(object __instance)
    {
        var active=Current?.run;
        if(active is not null && ReplayGameApi.Same(active.Info,__instance))Current!.Api.KeepSeed(__instance,active.Replay.Seed);
    }
    private static void ResetAfter(PlayInput __instance,int __0)
    {
        if(!Own(__instance))return;var active=Current!.run!;active.Seek=__0;
        if(active.Helper is not null)Current.Api.Seek(active.Helper,__0);
    }
    private static bool TickBefore(PlayInput __instance,int __0)
    {
        if(!Own(__instance))return true;
        var owner=Current!;var active=owner.run!;
        try
        {
            if(active.Helper is null)
            {
                var control=owner.Api.Control(__instance);if(control is null)throw new IOException("正常游玩输入尚未就绪。");
                active.Helper=owner.Api.Helper(active.Inputs);owner.Api.Attach(active.Helper,control);owner.Api.Seek(active.Helper,active.Seek);active.Control=control;
            }
            owner.Api.Tick(active.Helper,__0);
        }
        catch(Exception ex){owner.failure="重演已中止："+ex.GetBaseException().Message;}
        return false;
    }
#if BETTER_MALODYV_VERIFY
    internal string? TestFailure=>failure;
    [Il2CppInterop.Runtime.Attributes.HideFromIl2Cpp]
    internal void TestArm(object info,ReplayFile replay,ChartPlayer player)
    {run=new(info,replay,ReplayGameApi.InputsOnly(replay),Api.UserId){Player=player,Input=player.playInput};}
    internal void TestClear(){run=null;failure=null;}
#endif
}
