using System.Globalization;
using System.Reflection;
using HarmonyLib;
using Malody.DB;
using Malody.Play;
using Malody.Scene;
using Malody.Scene.Item;
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
    internal ReplayAnalysisGameApi AnalysisApi=null!;
    private Run? run;
    private float nextScan;
    private float nextAccountCheck;
    private float nextBaka;
    private string? failure;
    private const string BakaLabelName="BetterMalodyV.BakaAccuracy";
    private static readonly Color BakaColor=new(.32f,.68f,1f,1f);
    private readonly Queue<BakaJob> bakaQueue=new();
    private readonly HashSet<string> queuedBaka=new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string,(double Accuracy,bool TurboTail)?> bakaCache=new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<int,string> itemBakaPaths=new();
    private sealed record BakaJob(ItemScore Item,int InstanceId,string Path);
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
        AnalysisApi=new(Api);
        var installed=new List<MethodInfo>();
        HarmonyMethod Hook(string name)=>new(typeof(ReplayRunController),name);
        void Patch(MethodInfo method,string name,bool postfix=false)
        {installed.Add(method);harmony.Patch(method,prefix:postfix?null:Hook(name),postfix:postfix?Hook(name):null);}
        try
        {
            Patch(typeof(PanelScoreDetail).GetMethod("Show",new[]{typeof(ScoreRecord)})!,nameof(LocalShown),true);
            Patch(typeof(ItemScore).GetMethod("Fill")!,nameof(ScoreFilled),true);
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
        ProcessBakaQueue();
        if(Time.unscaledTime<nextScan)return;nextScan=Time.unscaledTime+.5f;
        foreach(var panel in UnityEngine.Object.FindObjectsOfType<PanelScoreDetail>())
            try {Attach(panel);}catch(Exception ex){Plugin.Current.LogRoom("Replay action unavailable: "+ex.GetBaseException().Message);}
    }
    private static void LocalShown(PanelScoreDetail __instance)
    {try {Current?.Attach(__instance);}catch(Exception ex){Plugin.Current.LogRoom("Replay action: "+ex.GetBaseException().Message);}}

    private static void ScoreFilled(ItemScore __instance,object __1)
    {
        try {Current?.QueueBaka(__instance,__1);}
        catch(Exception ex) {Plugin.Current.LogRoom("Replay Baka label unavailable: "+ex.GetBaseException().Message);}
    }

    [Il2CppInterop.Runtime.Attributes.HideFromIl2Cpp]
    private void QueueBaka(ItemScore item,object data)
    {
        var label=EnsureBakaLabel(item);
        if(!item || label is null)return;
        var instanceId=item.GetInstanceID();
        if(data is not ScoreRecord record || string.IsNullOrWhiteSpace(record.ReplayFile))
        {
            itemBakaPaths.Remove(instanceId);label.gameObject.SetActive(false);return;
        }
        var path=Api.ReplayArchivePath(record.ReplayFile);
        itemBakaPaths[instanceId]=path;label.gameObject.SetActive(true);label.color=BakaColor;
        if(bakaCache.TryGetValue(path,out var cached)){ApplyBaka(label,cached);return;}
        label.text="…";
        if(queuedBaka.Add(path))bakaQueue.Enqueue(new(item,instanceId,path));
    }

    [Il2CppInterop.Runtime.Attributes.HideFromIl2Cpp]
    private void ProcessBakaQueue()
    {
        if(Time.unscaledTime<nextBaka || bakaQueue.Count==0)return;
        nextBaka=Time.unscaledTime+.06f;
        var job=bakaQueue.Dequeue();queuedBaka.Remove(job.Path);
        (double Accuracy,bool TurboTail)? result=null;
        try {result=AnalysisApi.AnalyzeBaka(job.Path);}
        catch(Exception ex){Plugin.Current.LogRoom("Replay Baka analysis skipped: "+ex.GetBaseException().Message);}
        bakaCache[job.Path]=result;
        if(!itemBakaPaths.TryGetValue(job.InstanceId,out var current)
            || !string.Equals(current,job.Path,StringComparison.OrdinalIgnoreCase))return;
        var label=EnsureBakaLabel(job.Item);if(label is not null)ApplyBaka(label,result);
    }

    private static void ApplyBaka(Text label,(double Accuracy,bool TurboTail)? result)
    {
        label.gameObject.SetActive(result is not null);if(result is null)return;
        label.text=(result.Value.Accuracy*100).ToString("0.00",CultureInfo.InvariantCulture)+"%";
        label.color=BakaColor;
    }

    private static Text? EnsureBakaLabel(ItemScore item)
    {
        if(!item || !item.acc)return null;
        var existing=item.acc.transform.parent.Find(BakaLabelName)?.GetComponent<Text>();
        if(existing is not null)return existing;
        var source=item.acc;var go=new GameObject(BakaLabelName);go.layer=source.gameObject.layer;
        var label=go.AddComponent<Text>();label.font=source.font;label.fontSize=source.fontSize;label.fontStyle=source.fontStyle;
        label.alignment=TextAnchor.MiddleRight;label.horizontalOverflow=HorizontalWrapMode.Overflow;
        label.verticalOverflow=VerticalWrapMode.Truncate;label.raycastTarget=false;label.supportRichText=source.supportRichText;
        label.resizeTextForBestFit=source.resizeTextForBestFit;label.resizeTextMinSize=source.resizeTextMinSize;
        label.resizeTextMaxSize=source.resizeTextMaxSize;label.color=BakaColor;
        var rect=go.GetComponent<RectTransform>();rect.SetParent(source.transform.parent,false);
        var sourceRect=source.rectTransform;rect.anchorMin=sourceRect.anchorMin;rect.anchorMax=sourceRect.anchorMax;
        rect.pivot=new Vector2(1,sourceRect.pivot.y);rect.sizeDelta=new Vector2(Math.Max(88,sourceRect.sizeDelta.x*.58f),sourceRect.sizeDelta.y);
        rect.anchoredPosition=sourceRect.anchoredPosition+new Vector2(-8,0);label.gameObject.SetActive(false);return label;
    }
    [Il2CppInterop.Runtime.Attributes.HideFromIl2Cpp]
    internal Button? Attach(PanelScoreDetail panel)
    {
        const string name="BetterMalodyV.ReplayRun";
        var path=Api.LocalPath(panel);
        if(string.IsNullOrEmpty(path) || !panel.areaRep.activeSelf)return null;
        var existing=panel.areaRep.transform.Find(name);
        var analysisExisting=panel.areaRep.transform.Find("BetterMalodyV.ReplayAnalysis");
        var source=NativeUi.PrimaryText(panel.areaRep.transform);
        if(!source)return null;
        var native=panel.areaRep.GetComponentsInChildren<RectTransform>().Where(r=>r.parent==panel.areaRep.transform
            && r.name!=name && r.name!="BetterMalodyV.ReplayAnalysis" && r.rect.width>150 && r.rect.height>32
            && r.GetComponentInChildren<TextMeshProUGUI>()).ToArray();
        if(native.Length==0)return null;
        var label=source!.font.HasCharacter('重',true,true)?"重演并上传":"Replay & upload";
        var analysisLabel=source.font.HasCharacter('回',true,true)?"回放分析":"Replay analysis";
        var button=existing ? existing!.GetComponent<Button>() : NativeUi.Action(panel.areaRep.transform,source,label,name);
        var analysisButton=analysisExisting ? analysisExisting!.GetComponent<Button>()
            : NativeUi.Action(panel.areaRep.transform,source,analysisLabel,"BetterMalodyV.ReplayAnalysis");
        foreach(var action in new[]{button,analysisButton})
        {
            var rect=action!.GetComponent<RectTransform>();
            rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(.5f,.5f);
        }
        NativeUi.AlignActionRow(panel.areaRep.GetComponent<RectTransform>(),native.Concat(new[]
        {
            button!.GetComponent<RectTransform>(),analysisButton!.GetComponent<RectTransform>()
        }).ToArray());
        if(!existing)
            button!.onClick.AddListener((UnityAction)(()=>
            {
                if(run is not null)return;
                try {Launch(Api.LocalPath(panel)??throw new IOException("找不到本地回放文件。"));}
                catch(Exception ex){Plugin.Current.LogRoom("Replay launch stopped: "+ex.GetBaseException().Message);NativeUi.Message(ex.GetBaseException().Message);}
            }));
        if(!analysisExisting)
            analysisButton!.onClick.AddListener((UnityAction)(()=>
            {
                if(ReplayAnalysisView.IsOpen)return;
                try
                {
                    var file=Api.LocalPath(panel)??throw new IOException("找不到本地回放文件。");
                    var session=AnalysisApi.Analyze(file);
                    ReplayAnalysisView.Open(source,session);
                }
                catch(Exception ex){Plugin.Current.LogRoom("Replay analysis unavailable: "+ex.GetBaseException().Message);NativeUi.Message(ex.GetBaseException().Message);}
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
